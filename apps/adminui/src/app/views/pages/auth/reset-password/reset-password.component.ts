import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../../core/services/auth.service';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule
  ],
  templateUrl: './reset-password.component.html',
  styleUrl: './reset-password.component.scss'
})
export class ResetPasswordComponent implements OnInit {
  resetPasswordForm: FormGroup;
  loading = false;
  error = '';
  success = false;
  email = '';
  showPassword = false;
  showConfirmPassword = false;

  constructor(
    private formBuilder: FormBuilder,
    private router: Router,
    private authService: AuthService
  ) {
    this.resetPasswordForm = this.formBuilder.group({
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]]
    }, { validators: this.passwordMatchValidator });
  }

  ngOnInit(): void {
    // Get email from localStorage
    this.email = localStorage.getItem('resetEmail') || '';
    if (!this.email) {
      this.router.navigate(['/auth/forgot-password']);
      return;
    }
  }

  get f() { return this.resetPasswordForm.controls; }

  passwordMatchValidator(form: FormGroup) {
    const password = form.get('password');
    const confirmPassword = form.get('confirmPassword');

    if (password && confirmPassword && password.value !== confirmPassword.value) {
      confirmPassword.setErrors({ passwordMismatch: true });
    } else if (confirmPassword && confirmPassword.hasError('passwordMismatch')) {
      confirmPassword.setErrors(null);
    }

    return null;
  }

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  toggleConfirmPasswordVisibility(): void {
    this.showConfirmPassword = !this.showConfirmPassword;
  }

  onSubmit(): void {
    if (this.resetPasswordForm.invalid) {
      return;
    }

    this.loading = true;
    this.error = '';

    const { password } = this.resetPasswordForm.value;

    this.authService.resetPassword(this.email, password).subscribe({
      next: (response) => {
        this.loading = false;
        this.success = true;
        // Clear stored email
        localStorage.removeItem('resetEmail');
      },
      error: (error) => {
        this.loading = false;
        this.error = error.message || 'Failed to reset password. Please try again.';
      }
    });
  }

  goToLogin(): void {
    this.router.navigate(['/auth/login']);
  }

  hasUppercase(): boolean {
    const password = this.f['password'].value;
    return password && /[A-Z]/.test(password);
  }

  hasLowercase(): boolean {
    const password = this.f['password'].value;
    return password && /[a-z]/.test(password);
  }

  hasNumber(): boolean {
    const password = this.f['password'].value;
    return password && /[0-9]/.test(password);
  }
}
