import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

import { BusinessImpactComponent } from './business-impact.component';
import { AnalyticsService } from '../../../../core/services/analytics.service';

describe('BusinessImpactComponent', () => {
  let component: BusinessImpactComponent;
  let fixture: ComponentFixture<BusinessImpactComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        BusinessImpactComponent,
        HttpClientTestingModule,
        NoopAnimationsModule
      ],
      providers: [
        AnalyticsService
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(BusinessImpactComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with default filter settings', () => {
    expect(component.selectedPeriod).toBe('month');
    expect(component.loading).toBe(true);
  });

  it('should format currency correctly', () => {
    const result = component.formatCurrency(1234567);
    expect(result).toBeDefined();
  });

  it('should format percentage correctly', () => {
    const result = component.formatPercentage(75.5);
    expect(result).toBeDefined();
  });

  it('should get correct priority class for different priorities', () => {
    expect(component.getPriorityClass('Critical')).toBe('text-danger');
    expect(component.getPriorityClass('High')).toBe('text-warning');
    expect(component.getPriorityClass('Medium')).toBe('text-info');
    expect(component.getPriorityClass('Low')).toBe('text-secondary');
  });

  it('should get correct segment color class for value tiers', () => {
    expect(component.getSegmentColorClass('Premium')).toBe('badge-success');
    expect(component.getSegmentColorClass('High')).toBe('badge-info');
    expect(component.getSegmentColorClass('Medium')).toBe('badge-warning');
    expect(component.getSegmentColorClass('Low')).toBe('badge-secondary');
  });

  it('should convert satisfaction levels to numeric scores', () => {
    expect(component['getSatisfactionScore']('Low')).toBe(1);
    expect(component['getSatisfactionScore']('Medium')).toBe(2.5);
    expect(component['getSatisfactionScore']('High')).toBe(4);
    expect(component['getSatisfactionScore']('Excellent')).toBe(5);
  });
});