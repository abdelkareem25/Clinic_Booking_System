import { Routes } from '@angular/router';

import { UserDetailComponent } from './user-detail/user-detail.component';
import { UserListComponent } from './user-list/user-list.component';

export const USERS_ROUTES: Routes = [
  { path: '', component: UserListComponent, title: 'Users | Clinic Booking' },
  {
    path: 'me',
    component: UserDetailComponent,
    title: 'User Profile | Clinic Booking',
    data: { breadcrumb: 'Profile' }
  }
];
