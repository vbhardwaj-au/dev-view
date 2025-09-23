const { chromium } = require('playwright');

(async () => {
    const browser = await chromium.launch({
        headless: false,
        devtools: true
    });
    const context = await browser.newContext({
        ignoreHTTPSErrors: true
    });
    const page = await context.newPage();

    // Enable console logging
    page.on('console', msg => {
        console.log(`Browser Console [${msg.type()}]: ${msg.text()}`);
    });

    page.on('pageerror', err => {
        console.error(`Browser Error: ${err.message}`);
    });

    try {
        console.log('Navigating to login page...');
        await page.goto('http://localhost:5084/login');

        // Wait for page to load
        await page.waitForLoadState('networkidle');
        console.log('Login page loaded');

        // Take screenshot
        await page.screenshot({ path: 'login-page.png' });
        console.log('Screenshot saved as login-page.png');

        // Check if Azure AD button exists
        const azureButton = await page.locator('text="Sign in with Microsoft"').count();
        console.log(`Azure AD button found: ${azureButton > 0}`);

        if (azureButton > 0) {
            console.log('Clicking Azure AD sign-in button...');
            await page.click('text="Sign in with Microsoft"');

            // Wait for navigation
            await page.waitForTimeout(3000);
            console.log(`Current URL: ${page.url()}`);

            // Take screenshot after click
            await page.screenshot({ path: 'after-azure-click.png' });
        }

        // Also test database login
        console.log('\nTesting database login...');
        await page.goto('http://localhost:5084/login');
        await page.waitForLoadState('networkidle');

        // Try admin login
        await page.fill('input[placeholder="Enter username"]', 'admin');
        await page.fill('input[placeholder="Enter password"]', 'Admin#12345!');
        await page.click('button:has-text("Sign In")');

        await page.waitForTimeout(2000);
        console.log(`After database login - URL: ${page.url()}`);

        // Take screenshot
        await page.screenshot({ path: 'after-db-login.png' });

    } catch (error) {
        console.error('Test error:', error);
        await page.screenshot({ path: 'error-state.png' });
    }

    console.log('\nTest completed. Check the screenshots and logs above.');
    await browser.close();
})();