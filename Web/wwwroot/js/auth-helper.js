// Helper functions for JWT token management
window.authHelper = {
    saveToken: function(token) {
        localStorage.setItem('jwt-token', token);
    },
    
    getToken: function() {
        return localStorage.getItem('jwt-token');
    },
    
    removeToken: function() {
        localStorage.removeItem('jwt-token');
    },
    
    hasToken: function() {
        return !!localStorage.getItem('jwt-token');
    }
};