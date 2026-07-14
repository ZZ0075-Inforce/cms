/**
 * Development environment. The API is called directly on its own origin — there is no dev proxy,
 * which is why the API must send CORS headers for http://localhost:4200.
 */
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5000/api'
};
