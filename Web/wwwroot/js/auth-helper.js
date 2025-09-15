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
    },
    
    triggerAzureAdSignIn: function() {
        // Redirect to the Account sign-in endpoint which triggers Azure AD
        window.location.href = '/Account/SignIn';
    }
};