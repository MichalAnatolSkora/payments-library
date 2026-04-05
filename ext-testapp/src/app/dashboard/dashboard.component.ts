import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, P24Config } from '../services/api.service';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnInit {
  activeTab = 'config';
  result: any = null;
  loading = false;
  showRaw = false;

  config: P24Config = { merchantId: 0, posId: 0, apiKey: '', crcKey: '', sandbox: true };

  createPayment = {
    sessionId: '',
    amount: 100,
    currency: 'PLN',
    description: 'Test payment',
    customerEmail: 'test@example.com',
    returnUrl: 'http://localhost:5201',
    notifyUrl: 'http://localhost:5201/api/notify',
    customerName: 'Jan Kowalski',
    country: 'PL',
    language: 'pl',
  };

  statusSessionId = '';
  confirmPayment = { sessionId: '', providerId: '', amount: 100, currency: 'PLN' };
  refund = { sessionId: '', providerId: '', amount: 100, currency: 'PLN', description: 'Test refund' };
  notifications: any[] = [];

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {
    this.createPayment.sessionId = this.generateSessionId();
  }

  ngOnInit() {
    this.run(async () => {
      this.config = await this.api.loadServerConfig();
    });
  }

  generateSessionId(): string {
    return 'ext-' + Date.now() + '-' + Math.random().toString(36).substring(2, 8);
  }

  doCreatePayment() {
    this.run(async () => {
      this.result = await this.api.createPayment(this.createPayment, this.showRaw);
      const sid = this.createPayment.sessionId;
      this.statusSessionId = sid;
      this.confirmPayment.sessionId = sid;
      this.confirmPayment.amount = this.createPayment.amount;
      this.confirmPayment.currency = this.createPayment.currency;
      this.refund.sessionId = sid;
      this.refund.amount = this.createPayment.amount;
      this.refund.currency = this.createPayment.currency;
    });
  }

  doGetStatus() {
    this.run(async () => {
      this.result = await this.api.getPaymentStatus(this.statusSessionId, this.showRaw);
    });
  }

  doConfirmPayment() {
    this.run(async () => {
      this.result = await this.api.confirmPayment(this.confirmPayment, this.showRaw);
    });
  }

  doRefund() {
    this.run(async () => {
      this.result = await this.api.refund(this.refund, this.showRaw);
    });
  }

  doLoadNotifications() {
    this.run(async () => {
      this.notifications = await this.api.getNotifications();
      this.result = { count: this.notifications.length, notifications: this.notifications };
    });
  }

  /** Runs an async action with loading state and forced change detection. */
  private async run(action: () => Promise<void>) {
    this.loading = true;
    this.cdr.detectChanges();
    try {
      await action();
    } catch (e: any) {
      this.result = { error: e.message };
    }
    this.loading = false;
    this.cdr.detectChanges();
  }
}
