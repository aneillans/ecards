import Keycloak from 'keycloak-js';

const keycloakConfig = {
  url: window.ENV?.KEYCLOAK_URL || 'https://your-keycloak.example/',
  realm: window.ENV?.KEYCLOAK_REALM || 'your-realm',
  clientId: window.ENV?.KEYCLOAK_CLIENT || 'ecards-frontend'
};

const keycloak = new Keycloak(keycloakConfig);

let initialized = false;

export async function initAuth() {
  if (initialized) {
    console.log('Keycloak already initialized');
    return keycloak;
  }
  
  console.log('Starting Keycloak initialization...');
  
  try {
    // Use base path without query params for redirectUri
    const baseUrl = window.location.origin + window.location.pathname;
    console.log('Keycloak config:', { baseUrl, config: keycloakConfig });
    
    // Use login-required mode instead of check-sso to avoid slow SSO checks
    // This will only authenticate when explicitly needed (when user clicks login)
    const authenticated = await keycloak.init({ 
      onLoad: 'check-sso',
      pkceMethod: 'S256',
      checkLoginIframe: false, // Disable iframe check completely
      enableLogging: true, // Enable Keycloak logging for debugging
      flow: 'standard', // Use standard flow
      silentCheckSsoFallback: false // Don't use iframe fallback
    });
    
    initialized = true;
    console.log('Keycloak initialized successfully, authenticated:', authenticated);
    
    // Clean up OAuth parameters from URL after successful authentication
    if (authenticated && window.location.search) {
      const url = new URL(window.location.href);
      const paramsToRemove = ['state', 'session_state', 'iss', 'code'];
      let hasOAuthParams = false;
      
      paramsToRemove.forEach(param => {
        if (url.searchParams.has(param)) {
          url.searchParams.delete(param);
          hasOAuthParams = true;
        }
      });
      
      if (hasOAuthParams) {
        // Use replaceState to clean URL without triggering navigation
        window.history.replaceState({}, document.title, url.pathname + url.hash);
      }
    }
    
    return keycloak;
  }
  catch (e) {
    console.error('Keycloak init failed:', e);
    initialized = true; // Mark as initialized even on failure to prevent retries
    return keycloak;
  }
}

export function login(redirectUri) {
  // Ensure we always pass a string URL
  let redirect = redirectUri;
  if (!redirect || typeof redirect !== 'string') {
    redirect = window.location.origin + window.location.pathname;
  }
  
  console.log('Login with redirectUri:', redirect);
  return keycloak.login({ 
    redirectUri: redirect
  }); 
}
export function logout() { return keycloak.logout({ redirectUri: window.location.origin }); }
export function getToken() { return keycloak.token; }
export function updateToken(minValidity = 5) { return keycloak.updateToken(minValidity); }
export function isLoggedIn() { return !!keycloak.token; }
export function tokenParsed() { return keycloak.tokenParsed || {}; }
export function hasRole(role) {
  try {
    const parsed = tokenParsed();
    if (parsed && parsed.realm_access && Array.isArray(parsed.realm_access.roles)) return parsed.realm_access.roles.includes(role);
    return false;
  }
  catch { return false; }
}

export default keycloak;
