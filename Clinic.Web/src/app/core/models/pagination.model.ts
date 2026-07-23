export interface Pagination<T> {
  pageIndex: number;
  pageSize: number;
  count: number;
  data: T[];
}

export interface PageQuery {
  pageIndex?: number;
  pageSize?: number;
  sort?: string;
  search?: string;
}

export const DEFAULT_PAGE_INDEX = 1;
export const DEFAULT_PAGE_SIZE = 5;
export const MAX_PAGE_SIZE = 20;

