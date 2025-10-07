import { NgStyle, CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../../core/services/auth.service';
import { SignalRService } from '../../../../core/services/signalr.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    NgStyle,
    RouterLink,
    ReactiveFormsModule
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent implements OnInit {
  loginForm: FormGroup;
  returnUrl: string;
  loading = false;
  error = '';

  constructor(
    private router: Router,
    private route: ActivatedRoute,
    private authService: AuthService,
    private signalRService: SignalRService,
    private formBuilder: FormBuilder
  ) {
    this.loginForm = this.formBuilder.group({
      email: ['test@admin.com', [Validators.required, Validators.email]],
      password: ['Password123!', Validators.required]
    });
  }

  ngOnInit(): void {
    // Get the return URL from the route parameters, or default to dashboard
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/dashboard';

    // If already logged in, redirect
    if (this.authService.isAuthenticated) {
      this.router.navigate([this.returnUrl]);
    }
  }

  get f() { return this.loginForm.controls; }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      return;
    }

    this.loading = true;
    this.error = '';

    const { email, password } = this.loginForm.value;

    this.authService.login({ email, password }).subscribe({
      next: (response) => {
        this.loading = false;

        if (response.requiresPasswordChange) {
          // Redirect to password change page
          this.router.navigate(['/auth/change-password']);
        } else {
          // Start SignalR connection
          this.signalRService.startConnection();

          // Navigate to return URL
          this.router.navigate([this.returnUrl]);
        }
      },
      error: (error) => {
        this.loading = false;
        this.error = error.message || 'Login failed. Please try again.';
      }
    });
  }

  onLoggedin(e: Event) {
    // Keep the old method for backward compatibility with the existing template
    e.preventDefault();
    this.onSubmit();
  }
}
