import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../../core/services/auth.service';

@Component({
  selector: 'app-verify-otp',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink
  ],
  templateUrl: './verify-otp.component.html',
  styleUrl: './verify-otp.component.scss'
})
export class VerifyOTPComponent implements OnInit {
  verifyOTPForm: FormGroup;
  loading = false;
  error = '';
  email = '';
  resendTimer = 0;
  canResend = false;

  constructor(
    private formBuilder: FormBuilder,
    private router: Router,
    private authService: AuthService
  ) {
    this.verifyOTPForm = this.formBuilder.group({
      otp: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(6)]]
    });
  }

  ngOnInit(): void {
    // Get email from localStorage
    this.email = localStorage.getItem('resetEmail') || '';
    if (!this.email) {
      this.router.navigate(['/auth/forgot-password']);
      return;
    }

    // Start resend timer
    this.startResendTimer();
  }

  get f() { return this.verifyOTPForm.controls; }

  onOtpInput(event: any): void {
    const value = event.target.value.replace(/\D/g, ''); // Remove non-digits
    this.verifyOTPForm.patchValue({ otp: value });
  }

  startResendTimer(): void {
    this.resendTimer = 60; // 60 seconds
    this.canResend = false;

    const timer = setInterval(() => {
      this.resendTimer--;
      if (this.resendTimer <= 0) {
        clearInterval(timer);
        this.canResend = true;
      }
    }, 1000);
  }

  onSubmit(): void {
    if (this.verifyOTPForm.invalid) {
      return;
    }

    this.loading = true;
    this.error = '';

    const { otp } = this.verifyOTPForm.value;

    this.authService.verifyPasswordResetOTP(this.email, otp).subscribe({
      next: (response) => {
        this.loading = false;
        // Store OTP in localStorage for reset password page
        localStorage.setItem('resetOTP', otp);
        // Navigate to reset password page
        this.router.navigate(['/auth/reset-password']);
      },
      error: (error) => {
        this.loading = false;
        this.error = error.message || 'Invalid verification code. Please try again.';
      }
    });
  }

  resendCode(): void {
    if (!this.canResend) {
      return;
    }

    this.authService.sendPasswordResetOTP(this.email).subscribe({
      next: (response) => {
        this.startResendTimer();
      },
      error: (error) => {
        this.error = error.message || 'Failed to resend code. Please try again.';
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/auth/forgot-password']);
  }
}
