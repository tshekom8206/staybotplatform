import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../../core/services/auth.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink
  ],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss'
})
export class ForgotPasswordComponent {
  forgotPasswordForm: FormGroup;
  loading = false;
  success = false;
  error = '';

  constructor(
    private formBuilder: FormBuilder,
    public router: Router,
    private authService: AuthService
  ) {
    this.forgotPasswordForm = this.formBuilder.group({
      email: ['', [Validators.required, Validators.email]]
    });
  }

  get f() { return this.forgotPasswordForm.controls; }

  onSubmit(): void {
    if (this.forgotPasswordForm.invalid) {
      return;
    }

    this.loading = true;
    this.error = '';

    const { email } = this.forgotPasswordForm.value;

    this.authService.sendPasswordResetOTP(email).subscribe({
      next: (response) => {
        this.loading = false;
        this.success = true;
        // Store email for OTP verification step
        localStorage.setItem('resetEmail', email);
      },
      error: (error) => {
        this.loading = false;
        this.error = error.message || 'Failed to send reset code. Please try again.';
      }
    });
  }

  resendCode(): void {
    if (this.forgotPasswordForm.invalid) {
      return;
    }

    const { email } = this.forgotPasswordForm.value;

    this.authService.sendPasswordResetOTP(email).subscribe({
      next: (response) => {
        // Show success message for resend
        this.success = true;
      },
      error: (error) => {
        this.error = error.message || 'Failed to resend code. Please try again.';
      }
    });
  }
}
