using Clinic.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace Clinic.Tests.Middleware
{
    /// <summary>
    /// Unit tests for TODO #12 (finding C12).
    ///
    /// The security-critical behaviour is the Development/Production split: internals reach the
    /// caller in development and never outside it, while a traceId links what the caller saw to
    /// what the logs recorded.
    /// </summary>
    public sealed class GlobalExceptionHandlerTests
    {
        private const string SecretText = "Server=prod-sql;Password=hunter2";

        private static GlobalExceptionHandler CreateSut(string environmentName)
        {
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);

            return new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, environment.Object);
        }

        private static DefaultHttpContext CreateContext()
        {
            var context = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().BuildServiceProvider()
            };
            context.Request.Method = "GET";
            context.Request.Path = "/api/Patients/7";
            context.Response.Body = new MemoryStream();
            context.TraceIdentifier = "trace-abc";
            return context;
        }

        private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
        {
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            var json = await reader.ReadToEndAsync();

            Assert.False(string.IsNullOrWhiteSpace(json), "The handler wrote no response body.");

            return JsonDocument.Parse(json).RootElement.Clone();
        }

        [Fact]
        public async Task An_Unknown_Exception_Becomes_A_500_ProblemDetails()
        {
            var context = CreateContext();
            var sut = CreateSut(Environments.Production);

            var handled = await sut.TryHandleAsync(context, new InvalidOperationException("boom"), default);

            Assert.True(handled);
            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

            var body = await ReadBodyAsync(context);
            Assert.Equal(500, body.GetProperty("status").GetInt32());
            Assert.Equal("An unexpected error occurred.", body.GetProperty("title").GetString());
        }

        [Fact]
        public async Task The_Response_Is_Problem_Json()
        {
            var context = CreateContext();

            await CreateSut(Environments.Production).TryHandleAsync(context, new Exception("boom"), default);

            Assert.StartsWith("application/problem+json", context.Response.ContentType);
        }

        [Fact]
        public async Task A_TraceId_Is_Always_Present()
        {
            // The only thing linking a user's bug report to a log entry.
            var context = CreateContext();

            await CreateSut(Environments.Production).TryHandleAsync(context, new Exception("boom"), default);

            var body = await ReadBodyAsync(context);
            Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
        }

        [Fact]
        public async Task Production_Leaks_No_Internals()
        {
            // The whole point of not using the developer exception page in production.
            var context = CreateContext();
            var exception = new InvalidOperationException(SecretText, new Exception("inner detail"));

            await CreateSut(Environments.Production).TryHandleAsync(context, exception, default);

            context.Response.Body.Position = 0;
            var raw = await new StreamReader(context.Response.Body).ReadToEndAsync();

            Assert.DoesNotContain(SecretText, raw, StringComparison.Ordinal);
            Assert.DoesNotContain("inner detail", raw, StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(InvalidOperationException), raw, StringComparison.Ordinal);
            Assert.DoesNotContain("GlobalExceptionHandlerTests", raw, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Staging_Also_Leaks_No_Internals()
        {
            // Only Development is permitted to expose internals - not "anything that is not
            // Production".
            var context = CreateContext();

            await CreateSut(Environments.Staging)
                .TryHandleAsync(context, new InvalidOperationException(SecretText), default);

            context.Response.Body.Position = 0;
            Assert.DoesNotContain(SecretText,
                await new StreamReader(context.Response.Body).ReadToEndAsync(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task Development_Includes_The_Exception_For_Diagnosis()
        {
            var context = CreateContext();

            await CreateSut(Environments.Development)
                .TryHandleAsync(context, new InvalidOperationException("boom"), default);

            var body = await ReadBodyAsync(context);
            var detail = body.GetProperty("detail").GetString();

            Assert.Contains("boom", detail!, StringComparison.Ordinal);
            Assert.Contains(nameof(InvalidOperationException), detail!, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_Concurrency_Conflict_Becomes_409()
        {
            var context = CreateContext();

            await CreateSut(Environments.Production)
                .TryHandleAsync(context, new DbUpdateConcurrencyException("conflict"), default);

            Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

            var body = await ReadBodyAsync(context);
            Assert.Contains("modified by another user", body.GetProperty("title").GetString()!);
        }

        [Fact]
        public async Task A_Database_Constraint_Violation_Becomes_409()
        {
            var context = CreateContext();

            await CreateSut(Environments.Production)
                .TryHandleAsync(context, new DbUpdateException("unique index"), default);

            Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        }

        [Fact]
        public async Task An_Unauthorized_Access_Exception_Becomes_403()
        {
            var context = CreateContext();

            await CreateSut(Environments.Production)
                .TryHandleAsync(context, new UnauthorizedAccessException(), default);

            Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        }

        [Fact]
        public async Task A_Client_Disconnect_Is_Not_Treated_As_A_Server_Error()
        {
            // Otherwise a user hitting refresh fills the error log with 500s.
            var context = CreateContext();
            var aborted = new CancellationTokenSource();
            aborted.Cancel();
            context.RequestAborted = aborted.Token;

            var handled = await CreateSut(Environments.Production)
                .TryHandleAsync(context, new OperationCanceledException(), default);

            Assert.True(handled);
            Assert.Equal(499, context.Response.StatusCode);
        }

        [Fact]
        public async Task A_Cancellation_That_Is_Not_A_Client_Disconnect_Is_Still_An_Error()
        {
            // A timeout inside the server is a genuine fault, not a hang-up.
            var context = CreateContext();

            await CreateSut(Environments.Production)
                .TryHandleAsync(context, new OperationCanceledException(), default);

            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        }

        [Fact]
        public async Task Nothing_Is_Written_Once_The_Response_Has_Started()
        {
            // Writing then would corrupt a partially sent response; the server must tear it down.
            var context = new Mock<HttpContext>();
            var response = new Mock<HttpResponse>();
            response.SetupGet(r => r.HasStarted).Returns(true);
            context.SetupGet(c => c.Response).Returns(response.Object);
            context.SetupGet(c => c.Request).Returns(new DefaultHttpContext().Request);

            var handled = await CreateSut(Environments.Production)
                .TryHandleAsync(context.Object, new Exception("boom"), default);

            Assert.False(handled);
        }

        [Fact]
        public async Task The_Instance_Records_Which_Path_Failed()
        {
            var context = CreateContext();

            await CreateSut(Environments.Production).TryHandleAsync(context, new Exception("boom"), default);

            var body = await ReadBodyAsync(context);
            Assert.Equal("/api/Patients/7", body.GetProperty("instance").GetString());
        }
    }
}
