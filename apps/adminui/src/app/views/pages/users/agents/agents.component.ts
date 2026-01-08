import { Component, OnInit, OnDestroy, inject, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, takeUntil, forkJoin, interval } from 'rxjs';
import { NgbDropdownModule, NgbDropdownToggle, NgbDropdownMenu, NgbTooltipModule, NgbAlertModule, NgbModalModule, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FeatherIconDirective } from '../../../../core/feather-icon/feather-icon.directive';
import { AgentService, AgentStatus, AgentWorkload, AvailableAgent, AgentStats } from '../../../../core/services/agent.service';

@Component({
  selector: 'app-agents',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbDropdownToggle,
    NgbDropdownMenu,
    NgbTooltipModule,
    NgbAlertModule,
    NgbModalModule,
    FeatherIconDirective
  ],
  templateUrl: './agents.component.html',
  styleUrls: ['./agents.component.scss']
})
export class AgentsComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private modalService = inject(NgbModal);
  private agentService = inject(AgentService);
  private formBuilder = inject(FormBuilder);

  @ViewChild('statusModal', { static: true }) statusModalTemplate!: TemplateRef<any>;
  @ViewChild('transferModal', { static: true }) transferModalTemplate!: TemplateRef<any>;

  // Data properties
  agents: AvailableAgent[] = [];
  filteredAgents: AvailableAgent[] = [];
  agentWorkloads: Map<number, AgentWorkload> = new Map();
  stats: AgentStats | null = null;

  // UI state
  loading = true;
  error: string | null = null;
  successMessage: string | null = null;

  // Search and filters
  searchTerm = '';
  selectedStatus = '';
  selectedDepartment = '';
  activeTab = 'all';

  // Modal state
  statusForm: FormGroup;
  selectedAgent: AvailableAgent | null = null;
  modalRef: any;

  // Real-time update
  refreshInterval = interval(30000); // Refresh every 30 seconds

  constructor() {
    this.statusForm = this.formBuilder.group({
      status: ['Available', [Validators.required]],
      statusMessage: ['']
    });
  }

  ngOnInit() {
    this.loadData();
    this.startRealTimeUpdates();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData() {
    this.loading = true;
    this.error = null;

    forkJoin({
      agents: this.agentService.getAvailableAgents(),
      stats: this.agentService.getAgentStats()
    }).pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: async (data) => {
        this.agents = data.agents;
        this.stats = data.stats;

        // Load workload for each agent
        for (const agent of this.agents) {
          const workload = await this.agentService.getAgentWorkload(agent.agentId).toPromise();
          if (workload) {
            this.agentWorkloads.set(agent.agentId, workload);
          }
        }

        this.applyFilters();
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading agent data:', error);
        this.error = 'Failed to load agent data. Please try again.';
        this.loading = false;
      }
    });
  }

  startRealTimeUpdates() {
    this.refreshInterval.pipe(
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.loadData();
    });
  }

  applyFilters() {
    let filtered = [...this.agents];

    // Apply search filter
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(agent =>
        agent.name.toLowerCase().includes(term) ||
        agent.email.toLowerCase().includes(term) ||
        agent.department.toLowerCase().includes(term)
      );
    }

    // Apply status filter
    if (this.selectedStatus) {
      filtered = filtered.filter(agent => agent.state === this.selectedStatus);
    }

    // Apply department filter
    if (this.selectedDepartment) {
      filtered = filtered.filter(agent => agent.department === this.selectedDepartment);
    }

    // Apply tab filter
    switch (this.activeTab) {
      case 'available':
        filtered = filtered.filter(agent => agent.state === 'Available');
        break;
      case 'busy':
        filtered = filtered.filter(agent => agent.state === 'Busy');
        break;
      case 'offline':
        filtered = filtered.filter(agent => agent.state === 'Offline');
        break;
    }

    this.filteredAgents = filtered;
  }

  onSearch() {
    this.applyFilters();
  }

  onFilterChange() {
    this.applyFilters();
  }

  setActiveTab(tab: string) {
    this.activeTab = tab;
    this.applyFilters();
  }

  clearFilters() {
    this.searchTerm = '';
    this.selectedStatus = '';
    this.selectedDepartment = '';
    this.activeTab = 'all';
    this.applyFilters();
  }

  openStatusModal(agent: AvailableAgent) {
    this.selectedAgent = agent;
    this.statusForm.patchValue({
      status: agent.state,
      statusMessage: agent.statusMessage || ''
    });
    this.modalRef = this.modalService.open(this.statusModalTemplate, {
      backdrop: 'static',
      keyboard: false
    });
  }

  updateAgentStatus() {
    if (this.statusForm.invalid || !this.selectedAgent) {
      return;
    }

    const formData = this.statusForm.value;

    this.agentService.updateAgentStatus(
      this.selectedAgent.agentId,
      formData.status,
      formData.statusMessage
    ).pipe(takeUntil(this.destroy$))
    .subscribe({
      next: () => {
        this.showSuccessMessage('Agent status updated successfully');
        this.closeModal();
        this.loadData();
      },
      error: (error) => {
        this.error = error.error?.message || 'Failed to update agent status';
      }
    });
  }

  assignConversation(agentId: number, conversationId: number) {
    this.agentService.assignConversation(agentId, conversationId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showSuccessMessage('Conversation assigned successfully');
          this.loadData();
        },
        error: (error) => {
          this.error = error.error?.message || 'Failed to assign conversation';
        }
      });
  }

  releaseConversation(conversationId: number) {
    this.agentService.releaseConversation(conversationId, 'Manual release by admin')
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.showSuccessMessage('Conversation released successfully');
          this.loadData();
        },
        error: (error) => {
          this.error = error.error?.message || 'Failed to release conversation';
        }
      });
  }

  closeModal() {
    if (this.modalRef) {
      this.modalRef.close();
      this.modalRef = null;
    }
    this.error = null;
  }

  showSuccessMessage(message: string) {
    this.successMessage = message;
    setTimeout(() => {
      this.successMessage = null;
    }, 5000);
  }

  dismissError() {
    this.error = null;
  }

  dismissSuccess() {
    this.successMessage = null;
  }

  // Utility methods
  getStatusBadgeClass(state: string): string {
    const statusMap: { [key: string]: string } = {
      'Available': 'bg-success',
      'Busy': 'bg-warning',
      'Away': 'bg-secondary',
      'DoNotDisturb': 'bg-danger',
      'Offline': 'bg-dark'
    };
    return statusMap[state] || 'bg-secondary';
  }

  getWorkloadBadgeClass(workload: number, maxConcurrent: number): string {
    const percentage = (workload / maxConcurrent) * 100;
    if (percentage >= 90) return 'bg-danger';
    if (percentage >= 70) return 'bg-warning';
    if (percentage >= 50) return 'bg-info';
    return 'bg-success';
  }

  getAvailabilityText(score: number): string {
    if (score >= 0.8) return 'High';
    if (score >= 0.5) return 'Medium';
    if (score >= 0.2) return 'Low';
    return 'None';
  }

  getAvailabilityBadgeClass(score: number): string {
    if (score >= 0.8) return 'bg-success';
    if (score >= 0.5) return 'bg-warning';
    if (score >= 0.2) return 'bg-danger';
    return 'bg-dark';
  }

  formatLastActivity(date: Date): string {
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / 60000);

    if (minutes < 1) return 'Just now';
    if (minutes < 60) return `${minutes}m ago`;

    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;

    const days = Math.floor(hours / 24);
    return `${days}d ago`;
  }

  formatResponseTime(timeSpan: string): string {
    // Convert C# TimeSpan to readable format
    const parts = timeSpan.split(':');
    if (parts.length >= 2) {
      const minutes = parseInt(parts[1]);
      const seconds = parts.length > 2 ? parseInt(parts[2].split('.')[0]) : 0;
      if (minutes > 0) return `${minutes}m ${seconds}s`;
      return `${seconds}s`;
    }
    return '0s';
  }

  getDepartments(): string[] {
    return [...new Set(this.agents.map(agent => agent.department))].sort();
  }

  getStatusOptions(): string[] {
    return ['Available', 'Busy', 'Away', 'DoNotDisturb', 'Offline'];
  }

  getFilteredAgentCount(): number {
    return this.filteredAgents.length;
  }

  getTotalAgentCount(): number {
    return this.agents.length;
  }

  // Track by function for ngFor performance
  agentTrackBy(index: number, agent: AvailableAgent): number {
    return agent.agentId;
  }

  // Math utilities
  Math = Math;

  // Form validation helpers
  isFieldInvalid(fieldName: string): boolean {
    const field = this.statusForm.get(fieldName);
    return !!(field && field.invalid && field.touched);
  }

  getFieldError(fieldName: string): string {
    const field = this.statusForm.get(fieldName);
    if (field?.errors) {
      if (field.errors['required']) return `${fieldName} is required`;
    }
    return '';
  }

  getStateIcon(state: string): string {
    switch (state) {
      case 'Available':
        return 'check-circle';
      case 'Busy':
        return 'clock';
      case 'Away':
        return 'coffee';
      case 'DoNotDisturb':
        return 'bell-off';
      case 'Offline':
        return 'power';
      default:
        return 'user';
    }
  }

  getStateIconBgClass(state: string): string {
    switch (state) {
      case 'Available':
        return 'bg-success';
      case 'Busy':
        return 'bg-warning';
      case 'Away':
        return 'bg-secondary';
      case 'DoNotDisturb':
        return 'bg-danger';
      case 'Offline':
        return 'bg-dark';
      default:
        return 'bg-secondary';
    }
  }
}