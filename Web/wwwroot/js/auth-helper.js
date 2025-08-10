// Helper functions for JWT token management
window.authHelper = {
    saveToken: function(token) {
        localStorage.setItem('jwt-token', token);
        console.log('[AuthHelper] Token saved to localStorage');
    },
    
    getToken: function() {
        return localStorage.getItem('jwt-token');
    },
    
    removeToken: function() {
        localStorage.removeItem('jwt-token');
        console.log('[AuthHelper] Token removed from localStorage');
    },
    
    hasToken: function() {
        return !!localStorage.getItem('jwt-token');
    }
};