import { Injectable, computed } from '@angular/core';

import { toDateOnly } from '../utils/date.util';
import {
  DailyClosing,
  Expense,
  Invoice,
  InvoiceStatus,
  Payment,
  Refund,
  invoiceTotal,
  round2,
} from './finance.model';
import { LocalCollection, newId } from './local-collection';

export interface InvoiceWithBalance extends Invoice {
  total: number;
  paid: number;
  refunded: number;
  remaining: number;
  /** Recomputed from payments — the stored `status` is only ever draft/void. */
  effectiveStatus: InvoiceStatus;
}

export interface PeriodTotals {
  income: number;
  expenses: number;
  net: number;
  outstanding: number;
  invoiceCount: number;
  paymentCount: number;
}

/**
 * The clinic's books.
 *
 * Invoice status is *derived* from payments and refunds rather than stored,
 * because a stored status is exactly the field that goes stale when a payment
 * is recorded from another screen. Only `draft` and `void` are authored states;
 * everything else is a function of the money that actually moved.
 */
@Injectable({ providedIn: 'root' })
export class AccountsStore {
  private readonly invoicesCollection = new LocalCollection<Invoice>({
    key: 'invoices',
    version: 1,
    seed: () => [],
    searchFields: ['number', 'patientName'],
  });

  private readonly paymentsCollection = new LocalCollection<Payment>({
    key: 'payments',
    version: 1,
    seed: () => [],
    searchFields: ['invoiceNumber', 'patientName', 'reference'],
  });

  private readonly expensesCollection = new LocalCollection<Expense>({
    key: 'expenses',
    version: 1,
    seed: () => [],
    searchFields: ['description', 'vendor'],
  });

  private readonly refundsCollection = new LocalCollection<Refund>({
    key: 'refunds',
    version: 1,
    seed: () => [],
    searchFields: ['invoiceNumber', 'patientName', 'reason'],
  });

  private readonly closingsCollection = new LocalCollection<DailyClosing>({
    key: 'closings',
    version: 1,
    seed: () => [],
    searchFields: ['date'],
  });

  readonly payments = computed(() =>
    [...this.paymentsCollection.all()].sort((a, b) => b.paidAt.localeCompare(a.paidAt))
  );

  readonly expenses = computed(() =>
    [...this.expensesCollection.all()].sort((a, b) => b.spentAt.localeCompare(a.spentAt))
  );

  readonly refunds = computed(() =>
    [...this.refundsCollection.all()].sort((a, b) => b.refundedAt.localeCompare(a.refundedAt))
  );

  readonly closings = computed(() =>
    [...this.closingsCollection.all()].sort((a, b) => b.date.localeCompare(a.date))
  );

  /** Invoices with money applied, newest first. */
  readonly invoices = computed<InvoiceWithBalance[]>(() => {
    const paidByInvoice = new Map<string, number>();
    for (const payment of this.paymentsCollection.all()) {
      paidByInvoice.set(payment.invoiceId, (paidByInvoice.get(payment.invoiceId) ?? 0) + payment.amount);
    }

    const refundedByInvoice = new Map<string, number>();
    for (const refund of this.refundsCollection.all()) {
      refundedByInvoice.set(
        refund.invoiceId,
        (refundedByInvoice.get(refund.invoiceId) ?? 0) + refund.amount
      );
    }

    return this.invoicesCollection
      .all()
      .map((invoice) => {
        const total = invoiceTotal(invoice);
        const paid = round2(paidByInvoice.get(invoice.id) ?? 0);
        const refunded = round2(refundedByInvoice.get(invoice.id) ?? 0);
        const remaining = round2(Math.max(0, total - paid + refunded));

        return {
          ...invoice,
          total,
          paid,
          refunded,
          remaining,
          effectiveStatus: this.resolveStatus(invoice, total, paid, refunded),
        };
      })
      .sort((a, b) => b.issuedAt.localeCompare(a.issuedAt));
  });

  readonly outstanding = computed(() =>
    round2(
      this.invoices()
        .filter((invoice) => invoice.effectiveStatus === 'unpaid' || invoice.effectiveStatus === 'partial')
        .reduce((sum, invoice) => sum + invoice.remaining, 0)
    )
  );

  // ------------------------------------------------------------- reporting --

  /** Cash actually received, minus refunds, within `[from, to]` inclusive. */
  incomeBetween(from: Date, to: Date): number {
    const start = toDateOnly(from);
    const end = toDateOnly(to);

    const received = this.payments()
      .filter((payment) => within(payment.paidAt, start, end))
      .reduce((sum, payment) => sum + payment.amount, 0);

    const refunded = this.refunds()
      .filter((refund) => within(refund.refundedAt, start, end))
      .reduce((sum, refund) => sum + refund.amount, 0);

    return round2(received - refunded);
  }

  expensesBetween(from: Date, to: Date): number {
    const start = toDateOnly(from);
    const end = toDateOnly(to);

    return round2(
      this.expenses()
        .filter((expense) => within(expense.spentAt, start, end))
        .reduce((sum, expense) => sum + expense.amount, 0)
    );
  }

  totalsBetween(from: Date, to: Date): PeriodTotals {
    const start = toDateOnly(from);
    const end = toDateOnly(to);
    const income = this.incomeBetween(from, to);
    const expenses = this.expensesBetween(from, to);

    return {
      income,
      expenses,
      net: round2(income - expenses),
      outstanding: this.outstanding(),
      invoiceCount: this.invoices().filter((invoice) => within(invoice.issuedAt, start, end)).length,
      paymentCount: this.payments().filter((payment) => within(payment.paidAt, start, end)).length,
    };
  }

  /** Per-day income for the revenue chart. */
  dailyIncome(days: Date[]): { date: Date; value: number }[] {
    return days.map((date) => ({ date, value: this.incomeBetween(date, date) }));
  }

  // ------------------------------------------------------------ mutations --

  createInvoice(input: Omit<Invoice, 'id' | 'number'> & { number?: string }): Invoice {
    return this.invoicesCollection.insert({
      ...input,
      id: newId('inv'),
      number: input.number ?? this.nextInvoiceNumber(),
    });
  }

  updateInvoice(id: string, patch: Partial<Invoice>): void {
    this.invoicesCollection.update(id, patch);
  }

  removeInvoice(id: string): void {
    // Money attached to a deleted invoice would otherwise linger in the totals
    // forever, so its payments and refunds go with it.
    this.paymentsCollection.removeWhere((payment) => payment.invoiceId === id);
    this.refundsCollection.removeWhere((refund) => refund.invoiceId === id);
    this.invoicesCollection.remove(id);
  }

  recordPayment(input: Omit<Payment, 'id'>): Payment {
    return this.paymentsCollection.insert({ ...input, id: newId('pay') });
  }

  removePayment(id: string): void {
    this.paymentsCollection.remove(id);
  }

  recordExpense(input: Omit<Expense, 'id'>): Expense {
    return this.expensesCollection.insert({ ...input, id: newId('exp') });
  }

  removeExpense(id: string): void {
    this.expensesCollection.remove(id);
  }

  recordRefund(input: Omit<Refund, 'id'>): Refund {
    return this.refundsCollection.insert({ ...input, id: newId('rfn') });
  }

  removeRefund(id: string): void {
    this.refundsCollection.remove(id);
  }

  closingFor(date: Date): DailyClosing | undefined {
    const key = toDateOnly(date);
    return this.closings().find((closing) => closing.date === key);
  }

  closeDay(date: Date, openingBalance: number, closedBy: string, notes?: string): DailyClosing {
    const key = toDateOnly(date);
    const cashIn = this.incomeBetween(date, date);
    const cashOut = this.expensesBetween(date, date);

    const closing: DailyClosing = {
      id: newId('cls'),
      date: key,
      openingBalance,
      cashIn,
      cashOut,
      closingBalance: round2(openingBalance + cashIn - cashOut),
      closedBy,
      closedAt: new Date().toISOString(),
      notes,
    };

    // Re-closing a day replaces the earlier figure rather than adding a second.
    const existing = this.closingFor(date);
    if (existing) {
      this.closingsCollection.remove(existing.id);
    }

    return this.closingsCollection.insert(closing);
  }

  // -------------------------------------------------------------- internal --

  private resolveStatus(
    invoice: Invoice,
    total: number,
    paid: number,
    refunded: number
  ): InvoiceStatus {
    if (invoice.status === 'draft' || invoice.status === 'void') {
      return invoice.status;
    }
    if (refunded > 0 && refunded >= paid) {
      return 'refunded';
    }
    if (paid <= 0) {
      return 'unpaid';
    }
    // A hair under the total is still paid — cash rounding should not leave an
    // invoice stuck at "partial" over half a piastre.
    return paid + 0.005 >= total ? 'paid' : 'partial';
  }

  private nextInvoiceNumber(): string {
    const year = new Date().getFullYear();
    const prefix = `INV-${year}-`;

    const highest = this.invoicesCollection
      .all()
      .filter((invoice) => invoice.number.startsWith(prefix))
      .reduce((max, invoice) => Math.max(max, Number(invoice.number.slice(prefix.length)) || 0), 0);

    return `${prefix}${`${highest + 1}`.padStart(4, '0')}`;
  }
}

/** Compares the date portion only, so a time component cannot exclude a day. */
function within(isoValue: string, startDate: string, endDate: string): boolean {
  const day = isoValue.slice(0, 10);
  return day >= startDate && day <= endDate;
}
