import { Component, OnInit, OnDestroy, AfterViewChecked, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../../core/feather-icon/feather-icon.directive';
import { BusinessRulesService } from '../../services/business-rules.service';
import { AuditLogEntry, AuditLogFilter, AuditAction, EntityType } from '../../models/business-rules.models';
import * as feather from 'feather-icons';

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    NgbTooltipModule,
    FeatherIconDirective
  ],
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss'
})
export class AuditLogComponent implements OnInit, OnDestroy, AfterViewChecked {
  private destroy$ = new Subject<void>();
  private businessRulesService = inject(BusinessRulesService);

  auditEntries: AuditLogEntry[] = [];
  filteredEntries: AuditLogEntry[] = [];
  loading = true;
  error: string | null = null;
  tenantId = 1;

  // Filters
  searchTerm = '';
  actionFilter: AuditAction | 'all' = 'all';
  entityTypeFilter: EntityType | 'all' = 'all';
  dateFrom: string = '';
  dateTo: string = '';

  // Filter options
  actions: AuditAction[] = ['CREATE', 'UPDATE', 'DELETE', 'ACTIVATE', 'DEACTIVATE'];
  entityTypes: EntityType[] = ['ServiceBusinessRule', 'RequestItemRule', 'UpsellItem'];

  ngOnInit(): void {
    this.loadAuditLog();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  ngAfterViewChecked(): void {
    feather.replace();
  }

  private loadAuditLog(): void {
    this.loading = true;
    this.error = null;

    const filter = this.buildFilter();
    this.businessRulesService.getAuditLog(this.tenantId, filter)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (entries) => {
          this.auditEntries = entries;
          this.applyFilters();
          this.loading = false;
        },
        error: (error) => {
          console.error('Error loading audit log:', error);
          this.error = 'Failed to load audit log. Please try again.';
          this.loading = false;
        }
      });
  }

  private buildFilter(): AuditLogFilter {
    const filter: AuditLogFilter = {
      sortBy: 'timestamp',
      sortDirection: 'desc'
    };

    if (this.searchTerm) filter.searchTerm = this.searchTerm;
    if (this.actionFilter !== 'all') filter.action = this.actionFilter;
    if (this.entityTypeFilter !== 'all') filter.entityType = this.entityTypeFilter;
    if (this.dateFrom) filter.dateFrom = new Date(this.dateFrom);
    if (this.dateTo) filter.dateTo = new Date(this.dateTo);

    return filter;
  }

  applyFilters(): void {
    let filtered = [...this.auditEntries];

    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(entry =>
        entry.userName.toLowerCase().includes(term) ||
        entry.userEmail.toLowerCase().includes(term) ||
        entry.entityName?.toLowerCase().includes(term)
      );
    }

    if (this.actionFilter !== 'all') {
      filtered = filtered.filter(entry => entry.action === this.actionFilter);
    }

    if (this.entityTypeFilter !== 'all') {
      filtered = filtered.filter(entry => entry.entityType === this.entityTypeFilter);
    }

    if (this.dateFrom) {
      const fromDate = new Date(this.dateFrom);
      filtered = filtered.filter(entry => entry.timestamp >= fromDate);
    }

    if (this.dateTo) {
      const toDate = new Date(this.dateTo);
      toDate.setHours(23, 59, 59, 999);
      filtered = filtered.filter(entry => entry.timestamp <= toDate);
    }

    this.filteredEntries = filtered;
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.actionFilter = 'all';
    this.entityTypeFilter = 'all';
    this.dateFrom = '';
    this.dateTo = '';
    this.applyFilters();
  }

  getActionIcon(action: string): string {
    switch (action.toUpperCase()) {
      case 'CREATE': return 'plus-circle';
      case 'UPDATE': return 'edit';
      case 'DELETE': return 'trash-2';
      case 'ACTIVATE': return 'check-circle';
      case 'DEACTIVATE': return 'x-circle';
      case 'LOGIN': return 'log-in';
      default: return 'activity';
    }
  }

  getActionClass(action: string): string {
    switch (action.toUpperCase()) {
      case 'CREATE': return 'badge bg-success';
      case 'UPDATE': return 'badge bg-info';
      case 'DELETE': return 'badge bg-danger';
      case 'ACTIVATE': return 'badge bg-success';
      case 'DEACTIVATE': return 'badge bg-warning';
      case 'LOGIN': return 'badge bg-secondary';
      default: return 'badge bg-secondary';
    }
  }

  getEntityTypeLabel(entityType: EntityType): string {
    switch (entityType) {
      case 'ServiceBusinessRule': return 'Service Rule';
      case 'RequestItemRule': return 'Request Item Rule';
      case 'UpsellItem': return 'Upsell Item';
    }
  }

  refresh(): void {
    this.loadAuditLog();
  }
}
