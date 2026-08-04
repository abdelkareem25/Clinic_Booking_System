import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  Account,
  AccountQuery,
  CreateAccountRequest,
  UpdateAccountRequest
} from '../models/account.model';
import { Pagination } from '../models/pagination.model';
import { ApiService } from './api.service';

/**
 * Staff accounts. One method per API operation, same shape as every other
 * service here — deliberately thin, so the module can be reasoned about from
 * the controller alone.
 */
@Injectable({ providedIn: 'root' })
export class AccountsService {
  private readonly api = inject(ApiService);

  getAccounts(query: AccountQuery = {}): Observable<Pagination<Account>> {
    return this.api.get<Pagination<Account>>('Accounts', {
      PageIndex: query.pageIndex,
      PageSize: query.pageSize,
      Search: query.search,
      Role: query.role,
      Status: query.status,
      Sort: query.sort
    });
  }

  getAccount(id: string): Observable<Account> {
    return this.api.get<Account>(`Accounts/${id}`);
  }

  createAccount(payload: CreateAccountRequest): Observable<Account> {
    return this.api.post<Account>('Accounts', payload);
  }

  updateAccount(id: string, payload: UpdateAccountRequest): Observable<Account> {
    return this.api.put<Account>(`Accounts/${id}`, payload);
  }

  /** Soft delete on the API side; the row survives so audit stamps still resolve. */
  deleteAccount(id: string): Observable<void> {
    return this.api.delete<void>(`Accounts/${id}`);
  }
}
