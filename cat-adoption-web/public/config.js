// Runtime API configuration - served as static file
// This gets loaded BEFORE the app bundle, so we can use it globally
window.__APP_CONFIG__ = {
  API_URL: (function() {
    // If on Vercel production domain, use Railway backend
    if (window.location.hostname.includes('vercel.app')) {
      return 'https://cat-adoption-net-production.up.railway.app'
    }
    // Otherwise use local proxy
    return '/api'
  })()
}
console.log('App Config - API URL:', window.__APP_CONFIG__.API_URL)

