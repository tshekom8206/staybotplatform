export const environment = {
  production: true,
  apiUrl: '/api', // Use relative path in production
  hubUrl: '/hubs/tasks',
  tenantId: null, // Set after login
  theme: {
    primaryColor: '#25D466',
    primaryLight: '#E8FAF0',
    primaryDark: '#1BAB50',
    primaryRgb: '37, 212, 102'
  },
  pwa: {
    enabled: true,
    updateCheckInterval: 21600000, // 6 hours in milliseconds
    syncInterval: 300000, // 5 minutes in milliseconds
  },
  // VAPID public key for push notifications
  // Generated: October 1, 2025
  vapidPublicKey: 'BCtM1kh-NFEV_GTAOzjnUnURDCVUGmU_z2S46trTDCwoeStCSpkPOuzuk9ymA4IgZP3LtCDRfAdilyzaFkweO9s'
};