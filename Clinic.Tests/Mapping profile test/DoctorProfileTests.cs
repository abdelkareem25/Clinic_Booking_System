using AutoMapper;
using Clinic.Api;
using Microsoft.Extensions.Logging.Abstractions;
namespace Clinic.Tests.Mapping_profile_test
{
    public class DoctorProfileTests
    {
        // AAA
        [Fact]
        public void All_Mapping_Profiles_Should_Be_Valid()
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.AddMaps(typeof(Program).Assembly);
            }, NullLoggerFactory.Instance);

            configuration.AssertConfigurationIsValid();
        }
    }
}
