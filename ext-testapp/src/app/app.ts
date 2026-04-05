import { Component, NgZone, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ClerkService,
  ClerkSignInComponent,
  ClerkUserButtonComponent,
} from 'ngx-clerk';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  imports: [CommonModule, ClerkSignInComponent, ClerkUserButtonComponent],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnInit {
  isLoaded = false;
  isSignedIn = false;
  userName = '';

  constructor(
    private clerk: ClerkService,
    private cdr: ChangeDetectorRef,
    private ngZone: NgZone,
  ) {}

  ngOnInit() {
    this.clerk.__init({
      publishableKey: environment.clerkPublishableKey,
    });

    this.clerk.clerk$.subscribe((clerk) => {
      this.ngZone.run(() => {
        this.isLoaded = true;
        this.isSignedIn = !!clerk.user;
        this.userName = clerk.user?.firstName ?? '';
        this.cdr.detectChanges();
      });
    });
  }
}
