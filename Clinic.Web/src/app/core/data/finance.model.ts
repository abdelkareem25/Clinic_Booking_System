import { Identified } from './local-collection';

export type InvoiceStatus = 'draft' | 'unpaid' | 'partial' | 'paid' | 'refunded' | 'void';

export type PaymentMethod = 'cash' | 'card' | 'transfer' | 'wallet';

export type ExpenseCategory =
  | 'supplies'
  | 'salaries'
  | 'rent'
  | 'utilities'
  | 'equipment'
  | 'marketing'
  | 'other';

export interface InvoiceLineItem {
  description: string;
  quantity: number;
  unitPrice: number;
}

export interface Invoice extends Identified {
  /** Human-facing number, e.g. `INV-2026-0007`. */
  number: string;
  /** API patient id; kept as a number so it joins to the Patients endpoint. */
  patientId: number;
  /** Snapshot of the name at issue time — invoices must not change retroactively. */
  patientName: string;
  doctorId?: number;
  doctorName?: string;
  /** ISO date. */
  issuedAt: string;
  dueAt: string;
  items: InvoiceLineItem[];
  /** Absolute currency amount, not a percentage. */
  discount: number;
  taxRate: number;
  notes?: string;
  status: InvoiceStatus;
}

export interface Payment extends Identified {
  invoiceId: string;
  invoiceNumber: string;
  patientName: string;
  amount: number;
  method: PaymentMethod;
  reference?: string;
  /** ISO date-time. */
  paidAt: string;
  recordedBy: string;
}

export interface Expense extends Identified {
  description: string;
  category: ExpenseCategory;
  vendor?: string;
  amount: number;
  /** ISO date. */
  spentAt: string;
  recordedBy: string;
}

export interface Refund extends Identified {
  invoiceId: string;
  invoiceNumber: string;
  patientName: string;
  amount: number;
  reason: string;
  /** ISO date-time. */
  refundedAt: string;
  recordedBy: string;
}

export interface DailyClosing extends Identified {
  /** `YYYY-MM-DD`. */
  date: string;
  openingBalance: number;
  cashIn: number;
  cashOut: number;
  closingBalance: number;
  closedBy: string;
  closedAt: string;
  notes?: string;
}

// -----------------------------------------------------------------------------
// Money
//
// Totals are derived, never stored: a stored total silently goes stale the
// moment a line item, discount or tax rate changes.
// -----------------------------------------------------------------------------

export function invoiceSubtotal(invoice: Invoice): number {
  return invoice.items.reduce((sum, item) => sum + item.quantity * item.unitPrice, 0);
}

export function invoiceTax(invoice: Invoice): number {
  const taxable = Math.max(0, invoiceSubtotal(invoice) - invoice.discount);
  return round2(taxable * (invoice.taxRate / 100));
}

export function invoiceTotal(invoice: Invoice): number {
  const taxable = Math.max(0, invoiceSubtotal(invoice) - invoice.discount);
  return round2(taxable + invoiceTax(invoice));
}

/** Currency amounts must never accumulate binary float drift across a day. */
export function round2(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

export const INVOICE_STATUS_LABELS: Record<InvoiceStatus, string> = {
  draft: 'accounts.statusDraft',
  unpaid: 'accounts.statusUnpaid',
  partial: 'accounts.statusPartial',
  paid: 'accounts.statusPaid',
  refunded: 'accounts.statusRefunded',
  void: 'accounts.statusVoid',
};

export const PAYMENT_METHOD_LABELS: Record<PaymentMethod, string> = {
  cash: 'accounts.methodCash',
  card: 'accounts.methodCard',
  transfer: 'accounts.methodTransfer',
  wallet: 'accounts.methodWallet',
};

export const EXPENSE_CATEGORY_LABELS: Record<ExpenseCategory, string> = {
  supplies: 'accounts.categorySupplies',
  salaries: 'accounts.categorySalaries',
  rent: 'accounts.categoryRent',
  utilities: 'accounts.categoryUtilities',
  equipment: 'accounts.categoryEquipment',
  marketing: 'accounts.categoryMarketing',
  other: 'accounts.categoryOther',
};
