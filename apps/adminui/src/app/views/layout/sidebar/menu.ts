import { MenuItem } from './menu.model';

export const MENU: MenuItem[] = [
  {
    label: 'Main',
    isTitle: true
  },
  {
    label: 'Dashboard',
    icon: 'home',
    link: '/dashboard'
  },
  {
    label: 'Hotel Operations',
    isTitle: true
  },
  {
    label: 'Conversations',
    icon: 'message-circle',
    subItems: [
      {
        label: 'Active Chats',
        link: '/conversations/active',
        badge: {
          variant: 'primary',
          text: 'LIVE'
        }
      },
      {
        label: 'Agent Assignments',
        link: '/conversations/assignments'
      },
      {
        label: 'Transfer Queue',
        link: '/conversations/transfers'
      },
      {
        label: 'Conversation History',
        link: '/conversations/history'
      }
    ]
  },
  {
    label: 'Guests',
    icon: 'users',
    subItems: [
      {
        label: 'Bookings',
        link: '/guests/bookings'
      },
      {
        label: 'Check-ins Today',
        link: '/guests/checkins'
      },
      {
        label: 'Guest History',
        link: '/guests/history'
      },
    ]
  },
  {
    label: 'Tasks',
    icon: 'clipboard',
    subItems: [
      {
        label: 'All Tasks',
        link: '/tasks/all'
      },
      {
        label: 'My Tasks',
        link: '/tasks/my'
      },
      {
        label: 'Housekeeping',
        link: '/tasks/housekeeping'
      },
      {
        label: 'Maintenance',
        link: '/tasks/maintenance'
      },
      {
        label: 'Front Desk',
        link: '/tasks/frontdesk'
      }
    ]
  },
  {
    label: 'Broadcast',
    icon: 'radio',
    subItems: [
      {
        label: 'Send Message',
        link: '/broadcast/compose'
      },
      {
        label: 'Emergency Alert',
        link: '/broadcast/emergency',
        badge: {
          variant: 'danger',
          text: 'URGENT'
        }
      },
      {
        label: 'Templates',
        link: '/broadcast/templates'
      },
      {
        label: 'History',
        link: '/broadcast/history'
      }
    ]
  },
  {
    label: 'Management',
    isTitle: true
  },
  {
    label: 'Configuration',
    icon: 'settings',
    subItems: [
      {
        label: 'Hotel Information',
        link: '/configuration/hotel-info'
      },
      {
        label: 'Services & Amenities',
        link: '/configuration/services'
      },
      {
        label: 'FAQs',
        link: '/configuration/faqs'
      },
      {
        label: 'Menu Items',
        link: '/configuration/menu'
      },
      {
        label: 'Emergency Settings',
        link: '/configuration/emergency'
      },
      {
        label: 'Template Manager',
        link: '/configuration/templates'
      }
    ]
  },
  {
    label: 'Users',
    icon: 'user-plus',
    subItems: [
      {
        label: 'Staff Management',
        link: '/users/staff'
      },
      {
        label: 'Agent Dashboard',
        link: '/users/agents'
      },
      {
        label: 'Roles & Permissions',
        link: '/users/roles'
      },
      {
        label: 'Activity Log',
        link: '/users/activity'
      }
    ]
  },
  {
    label: 'Reports',
    icon: 'bar-chart-2',
    subItems: [
      {
        label: 'Analytics Dashboard',
        link: '/reports/analytics'
      },
      {
        label: 'Task Performance',
        link: '/reports/tasks'
      },
      {
        label: 'Guest Satisfaction',
        link: '/reports/satisfaction'
      },
      {
        label: 'Service Usage',
        link: '/reports/usage'
      }
    ]
  },
  {
    label: 'Analytics',
    icon: 'trending-up',
    subItems: [
      {
        label: 'Business Impact',
        link: '/analytics/business-impact'
      }
    ]
  },
  {
    label: 'Support',
    isTitle: true
  },
  {
    label: 'Help',
    icon: 'help-circle',
    subItems: [
      {
        label: 'Documentation',
        link: '/help/docs'
      },
      {
        label: 'Video Tutorials',
        link: '/help/videos'
      },
      {
        label: 'Contact Support',
        link: '/help/support'
      }
    ]
  },
];
