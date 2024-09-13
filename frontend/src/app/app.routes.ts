import { Routes } from '@angular/router';
import { SessionsComponent } from './sessions/sessions.component';

export const routes: Routes = [
    { path: '', redirectTo: '/sessions', pathMatch: 'full' },
    { path: 'sessions', redirectTo: `/sessions/today`, pathMatch: 'full' },
    { path: 'sessions/:day', component: SessionsComponent }
];
