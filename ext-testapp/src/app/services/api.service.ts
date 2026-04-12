import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface P24Config {
  merchantId: number;
  posId: number;
  issandbox: boolean;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private config: P24Config = {
    merchantId: 0,
    posId: 0,
    issandbox: true,
  };

  constructor(private http: HttpClient) {}

  getConfig(): P24Config {
    return this.config;
  }

  async loadServerConfig(): Promise<P24Config> {
    const data = await firstValueFrom(this.http.get<any>('/api/config'));
    this.config = {
      merchantId: data.merchantId ?? 0,
      posId: data.posId ?? 0,
      issandbox: data.issandbox ?? true,
    };
    return this.config;
  }

  async createPayment(body: any, raw = false): Promise<any> {
    const url = raw ? '/api/create-payment-raw' : '/api/create-payment';
    return firstValueFrom(this.http.post(url, body));
  }

  async getPaymentStatus(sessionId: string, raw = false): Promise<any> {
    const url = raw ? `/api/payment-status-raw/${sessionId}` : `/api/payment-status/${sessionId}`;
    return firstValueFrom(this.http.get(url));
  }

  async confirmPayment(body: any, raw = false): Promise<any> {
    const url = raw ? '/api/confirm-payment-raw' : '/api/confirm-payment';
    return firstValueFrom(this.http.post(url, body));
  }

  async refund(body: any, raw = false): Promise<any> {
    const url = raw ? '/api/refund-raw' : '/api/refund';
    return firstValueFrom(this.http.post(url, body));
  }

  async getNotifications(): Promise<any> {
    return firstValueFrom(this.http.get('/api/notifications'));
  }
}
