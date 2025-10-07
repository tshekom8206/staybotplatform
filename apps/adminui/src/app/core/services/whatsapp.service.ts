import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface SendMessageRequest {
  phoneNumber: string;
  message: string;
  taskId?: number;
}

export interface SendMessageResponse {
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class WhatsAppService {

  constructor(private apiService: ApiService) {}

  /**
   * Send WhatsApp message to guest
   */
  sendMessage(request: SendMessageRequest): Observable<SendMessageResponse> {
    return this.apiService.post<SendMessageResponse>('guest/messages/send', {
      phoneNumber: request.phoneNumber,
      message: request.message
    });
  }
}