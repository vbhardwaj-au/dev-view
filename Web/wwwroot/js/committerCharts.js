// Use window scope to avoid variable redeclaration issues
window.committerCharts = {};
window.topCommittersDotNetRef = null;

window.setTopCommittersDotNetRef = (dotNetRef) => {
    window.topCommittersDotNetRef = dotNetRef;
};

window.openExternalUrl = (url, target = '_blank') => {
    try {
        const newWindow = window.open(url, target);
        if (newWindow) {
            newWindow.focus();
            console.log(`Successfully opened URL: ${url}`);
        } else {
            console.warn(`Failed to open URL: ${url}. Pop-up blocked or similar issue.`);
            alert(`Failed to open link. Please check your browser's pop-up blocker settings or try again.\n\nURL: ${url}`);
        }
    } catch (error) {
        console.error(`Error opening URL ${url}:`, error);
        alert(`An error occurred while trying to open the link. Please try again.\n\nURL: ${url}`);
    }
};

// Function to wait for DOM element with retries
window.waitForElement = (elementId, maxRetries = 10, retryDelay = 100) => {
    return new Promise((resolve, reject) => {
        let retries = 0;
        
        const checkElement = () => {
            const element = document.getElementById(elementId);
            if (element) {
                resolve(element);
            } else if (retries < maxRetries) {
                retries++;
                // console.log(`Waiting for element ${elementId}, retry ${retries}/${maxRetries}`); // Commented out for less noise
                setTimeout(checkElement, retryDelay);
            } else {
                reject(new Error(`Element ${elementId} not found after ${maxRetries} retries`));
            }
        };
        
        checkElement();
    });
};

window.initializeCommitterChart = async (canvasId, rawData, displayName, isTopCommitter, dotNetRef) => {
    try {
        // console.log('Initializing chart:', canvasId, 'Raw Data:', rawData); // Commented out for less noise
        
        // Parse data if it's a string
        let data;
        try {
            data = typeof rawData === 'string' ? JSON.parse(rawData) : rawData;
            // console.log('Parsed data:', data); // Commented out for less noise
        } catch (parseError) {
            console.error('Error parsing data:', parseError);
            return;
        }
        
        // Validate data
        if (!Array.isArray(data)) {
            console.error('Data is not an array after parsing:', typeof data, data);
            return;
        }
        
        if (data.length === 0) {
            console.warn('Data array is empty for canvas:', canvasId);
            return;
        }

        // Wait for Chart.js to be loaded
        if (typeof Chart === 'undefined') {
            console.warn('Chart.js not loaded, waiting...');
            await new Promise(resolve => setTimeout(resolve, 500));
            if (typeof Chart === 'undefined') {
                throw new Error('Chart.js failed to load');
            }
        }

        // Wait for the canvas element
        const chartElement = await window.waitForElement(canvasId);
        
        // Destroy existing chart if it exists (check both storage locations)
        if (window.committerCharts[canvasId] && typeof window.committerCharts[canvasId].destroy === 'function') {
            window.committerCharts[canvasId].destroy();
            delete window.committerCharts[canvasId];
        }
        
        // Also check if chart exists in contributorCharts (Dashboard uses this)
        const userId = canvasId.replace('contributorChart_', '');
        if (window.contributorCharts && window.contributorCharts[userId] && typeof window.contributorCharts[userId].destroy === 'function') {
            window.contributorCharts[userId].destroy();
            delete window.contributorCharts[userId];
        }
        
        // Get the existing chart instance from Chart.js directly and destroy it
        const existingChart = Chart.getChart(chartElement);
        if (existingChart) {
            existingChart.destroy();
        }

        // Format dates based on grouping
        const grouping = window.selectedGrouping || 'Day';
        const labels = data.map(d => {
            const date = new Date(d.date);
            if (isNaN(date.getTime())) {
                console.warn('Invalid date:', d.date);
                return 'Invalid Date';
            }
            switch (grouping) {
                case 'Month':
                    return date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
                case 'Week':
                    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
                case 'Day':
                default:
                    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
            }
        });

        // Prepare data with proper scaling
        const commitCounts = data.map(d => d.commitCount || 0);
        const totalAdded = data.map(d => d.totalLinesAdded || 0);
        const totalRemoved = data.map(d => d.totalLinesRemoved || 0);
        const codeAdded = data.map(d => d.codeLinesAdded || 0);
        const codeRemoved = data.map(d => d.codeLinesRemoved || 0);
        const dataAdded = data.map(d => d.dataLinesAdded || 0);
        const dataRemoved = data.map(d => d.dataLinesRemoved || 0);
        const configAdded = data.map(d => d.configLinesAdded || 0);
        const configRemoved = data.map(d => d.configLinesRemoved || 0);

        // Create chart with Dashboard-style configuration
        const ctx = chartElement.getContext('2d');
        window.committerCharts[canvasId] = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Commits',
                        data: commitCounts,
                        borderColor: 'rgb(54, 162, 235)',
                        backgroundColor: 'rgba(54, 162, 235, 0.1)',
                        fill: true,
                        tension: 0.4,
                        pointRadius: 3,
                        pointHoverRadius: 6,
                        borderWidth: 2,
                        hidden: true
                    },
                    {
                        label: 'Total ++',
                        data: totalAdded,
                        borderColor: 'rgb(34, 197, 94)',
                        backgroundColor: 'rgba(34, 197, 94, 0.1)',
                        fill: false,
                        tension: 0.4,
                        pointRadius: 2,
                        pointHoverRadius: 5,
                        borderWidth: 1.5,
                        hidden: true
                    },
                    {
                        label: 'Total --',
                        data: totalRemoved,
                        borderColor: 'rgb(239, 68, 68)',
                        backgroundColor: 'rgba(239, 68, 68, 0.1)',
                        fill: false,
                        tension: 0.4,
                        pointRadius: 2,
                        pointHoverRadius: 5,
                        borderWidth: 1.5,
                        hidden: true
                    },
                    {
                        label: '🧑‍💻 Code ++',
                        data: codeAdded,
                        borderColor: 'rgb(6, 182, 212)',
                        backgroundColor: 'rgba(6, 182, 212, 0.1)',
                        fill: false,
                        tension: 0.4,
                        pointRadius: 2,
                        pointHoverRadius: 5,
                        borderWidth: 1.5,
                        hidden: false
                    },
                    {
                        label: '🧑‍💻 Code --',
                        data: codeRemoved,
                        borderColor: 'rgb(236, 72, 153)',
                        backgroundColor: 'rgba(236, 72, 153, 0.1)',
                        fill: false,
                        tension: 0.4,
                        pointRadius: 2,
                        pointHoverRadius: 5,
                        borderWidth: 1.5,
                        hidden: false
                    },
                    {
                        label: '🗄️ Data ++',
                        data: dataAdded,
                        borderColor: 'rgb(147, 51, 234)',
                        backgroundColor: 'rgba(147, 51, 234, 0.1)',
                        fill: false,
                        tension: 0.4,
                        pointRadius: 2,
                        pointHoverRadius: 5,
                        borderWidth: 1.5,
                        hidden: true
                    },
                    {
                        label: '🗄️ Data --',
                        data: dataRemoved,
                        borderColor: 'rgb(249, 115, 22)',
                        backgroundColor: 'rgba(249, 115, 22, 0.1)',
                        fill: false,
                        tension: 0.4,
                        pointRadius: 2,
                        pointHoverRadius: 5,
                        borderWidth: 1.5,
                        hidden: true
                    },
                    {
                        label: '🛠️ Config ++',
                        data: configAdded,
                        borderColor: 'rgb(234, 179, 8)',
                        backgroundColor: 'rgba(234, 179, 8, 0.1)',
                        fill: false,
                        tension: 0.4,
                        pointRadius: 2,
                        pointHoverRadius: 5,
                        borderWidth: 1.5,
                        hidden: true
                    },
                    {
                        label: '🛠️ Config --',
                        data: configRemoved,
                        borderColor: 'rgb(154, 52, 18)',
                        backgroundColor: 'rgba(154, 52, 18, 0.1)',
                        fill: false,
                        tension: 0.4,
                        pointRadius: 2,
                        pointHoverRadius: 5,
                        borderWidth: 1.5,
                        hidden: true
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: true,
                        position: 'bottom',
                        labels: {
                            usePointStyle: true,
                            pointStyle: 'circle',
                            font: {
                                size: 10
                            },
                            padding: 10,
                            filter: function(item, chart) {
                                // Show only one legend item per category (hide the -- variants)
                                const label = item.text;
                                return !label.includes('--');
                            },
                            generateLabels: function(chart) {
                                const datasets = chart.data.datasets;
                                const labels = [];
                                
                                // Create custom legend items
                                const legendItems = [
                                    { index: 0, label: 'Commits', color: 'rgb(54, 162, 235)' },
                                    { 
                                        index: 1, 
                                        label: 'Total', 
                                        color: 'rgb(34, 197, 94)', // Use ++ color as primary
                                        pairedIndex: 2 // Index of the -- dataset
                                    },
                                    { 
                                        index: 3, 
                                        label: '🧑‍💻 Code', 
                                        color: 'rgb(6, 182, 212)',
                                        pairedIndex: 4
                                    },
                                    { 
                                        index: 5, 
                                        label: '🗄️ Data', 
                                        color: 'rgb(147, 51, 234)',
                                        pairedIndex: 6
                                    },
                                    { 
                                        index: 7, 
                                        label: '🛠️ Config', 
                                        color: 'rgb(234, 179, 8)',
                                        pairedIndex: 8
                                    }
                                ];
                                
                                return legendItems.map(item => ({
                                    text: item.label,
                                    fillStyle: item.color,
                                    strokeStyle: item.color,
                                    lineWidth: 2,
                                    pointStyle: 'circle',
                                    hidden: item.pairedIndex !== undefined ? 
                                        (datasets[item.index].hidden && datasets[item.pairedIndex].hidden) :
                                        datasets[item.index].hidden,
                                    datasetIndex: item.index,
                                    pairedDatasetIndex: item.pairedIndex
                                }));
                            }
                        },
                        onClick: function(evt, legendItem, legend) {
                            const chart = legend.chart;
                            const datasetIndex = legendItem.datasetIndex;
                            const pairedDatasetIndex = legendItem.pairedDatasetIndex;
                            
                            if (pairedDatasetIndex !== undefined) {
                                // Toggle both ++ and -- datasets together
                                const dataset1 = chart.data.datasets[datasetIndex];
                                const dataset2 = chart.data.datasets[pairedDatasetIndex];
                                const newHiddenState = !dataset1.hidden;
                                
                                dataset1.hidden = newHiddenState;
                                dataset2.hidden = newHiddenState;
                            } else {
                                // Toggle single dataset (Commits)
                                const dataset = chart.data.datasets[datasetIndex];
                                dataset.hidden = !dataset.hidden;
                            }
                            
                            chart.update();
                        }
                    },
                    tooltip: {
                        callbacks: {
                            title: function(context) {
                                const dataIndex = context[0].dataIndex;
                                const date = new Date(data[dataIndex].date);
                                return date.toLocaleDateString('en-US', { 
                                    weekday: 'short', 
                                    month: 'short', 
                                    day: 'numeric' 
                                });
                            },
                            label: function(context) {
                                const dataIndex = context.dataIndex;
                                const item = data[dataIndex];
                                const label = context.dataset.label;
                                switch(label) {
                                    case 'Commits':
                                        return `📝 Commits: ${item.commitCount}`;
                                    case 'Total ++':
                                        return `➕ Total ++: ${item.totalLinesAdded.toLocaleString()} lines`;
                                    case 'Total --':
                                        return `➖ Total --: ${item.totalLinesRemoved.toLocaleString()} lines`;
                                    case '🧑‍💻 Code ++':
                                        return `🧑‍💻 Code ++: ${item.codeLinesAdded.toLocaleString()} lines`;
                                    case '🧑‍💻 Code --':
                                        return `🧑‍💻 Code --: ${item.codeLinesRemoved.toLocaleString()} lines`;
                                    case '🗄️ Data ++':
                                        return `🗄️ Data ++: ${item.dataLinesAdded.toLocaleString()} lines`;
                                    case '🗄️ Data --':
                                        return `🗄️ Data --: ${item.dataLinesRemoved.toLocaleString()} lines`;
                                    case '🛠️ Config ++':
                                        return `🛠️ Config ++: ${item.configLinesAdded.toLocaleString()} lines`;
                                    case '🛠️ Config --':
                                        return `🛠️ Config --: ${item.configLinesRemoved.toLocaleString()} lines`;
                                    default:
                                        return `${label}: ${context.parsed.y.toLocaleString()}`;
                                }
                            }
                        },
                        onClick: (event, elements) => {
                            if (elements.length > 0) {
                                const dataIndex = elements[0].index;
                                const item = data[dataIndex];
                                const date = new Date(item.date).toISOString().split('T')[0]; // Format as YYYY-MM-DD
                                
                                if (dotNetRef) {
                                    dotNetRef.invokeMethodAsync('HandleChartClick', date);
                                }
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        display: true,
                        grid: {
                            display: false
                        },
                        ticks: {
                            maxTicksLimit: 6,
                            font: {
                                size: 10
                            }
                        }
                    },
                    y: {
                        display: true,
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.1)'
                        },
                        ticks: {
                            font: {
                                size: 10
                            }
                        }
                    }
                }
            }
        });
        
        // console.log(`Successfully initialized chart for ${canvasId}`); // Commented out for less noise
    } catch (error) {
        console.error('Error initializing chart:', canvasId, error);
    }
};

window.toggleCommitterDataset = function(canvasId, datasetIndex) {
    const chart = window.committerCharts[canvasId];
    if (!chart) {
        console.warn('No chart found for', canvasId);
        return;
    }
    const dataset = chart.data.datasets[datasetIndex];
    dataset.hidden = !dataset.hidden;
    chart.update();
}; 

window.renderPrsMergedByWeekdayChart = async (chartData) => {
    try {
        if (!Array.isArray(chartData) || chartData.length === 0) {
            console.warn('No PRs merged by weekday data provided or data is empty.');
            return;
        }

        if (typeof Chart === 'undefined') {
            console.warn('Chart.js not loaded, waiting before rendering PRs Merged by Weekday chart...');
            await new Promise(resolve => setTimeout(resolve, 500));
            if (typeof Chart === 'undefined') {
                console.error('Chart.js failed to load, cannot render PRs Merged by Weekday chart.');
                return;
            }
        }

        const chartElement = await window.waitForElement('prsMergedByWeekdayChart');
        if (window.committerCharts['prsMergedByWeekdayChart']) {
            window.committerCharts['prsMergedByWeekdayChart'].destroy();
        }

        const labels = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
        const data = labels.map(label => {
            const dayData = chartData.find(d => d.dayOfWeek === label);
            return dayData ? dayData.prCount : 0;
        });

        const maxValue = Math.max(...data);
        const backgroundColors = data.map(value => 
            value === maxValue ? 'rgba(22, 163, 74, 1)' : 'rgba(107, 114, 128, 0.2)'
        );
         const hoverBackgroundColors = data.map(value => 
            value === maxValue ? 'rgba(22, 163, 74, 0.9)' : 'rgba(107, 114, 128, 0.3)'
        );


        const ctx = chartElement.getContext('2d');
        window.committerCharts['prsMergedByWeekdayChart'] = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels.map(l => l.substring(0, 1)),
                datasets: [{
                    label: 'Number of PRs',
                    data: data,
                    backgroundColor: backgroundColors,
                    borderColor: 'transparent',
                    borderWidth: 0,
                    borderRadius: 5,
                    hoverBackgroundColor: hoverBackgroundColors,
                    barPercentage: 0.5,
                    categoryPercentage: 0.8
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        enabled: true,
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        titleFont: { size: 14, weight: 'bold' },
                        bodyFont: { size: 12 },
                        callbacks: {
                            label: function(context) {
                                return `PRs: ${context.raw}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            font: {
                                size: 12,
                                family: 'Inter, sans-serif'
                            }
                        }
                    },
                    y: {
                        beginAtZero: true,
                        grid: {
                            display: false
                        },
                        ticks: {
                            display: false
                        }
                    }
                }
            }
        });
    } catch (error) {
        console.error('Error rendering PRs Merged by Weekday chart:', error);
    }
}; 

window.renderPrAgeChart = async (chartData, filterDays = 0) => {
    try {
        if (!chartData || (!chartData.openPrAge?.length && !chartData.mergedPrAge?.length)) {
            console.warn('No PR age data provided or data is empty.');
            return;
        }

        if (typeof Chart === 'undefined') {
            console.warn('Chart.js not loaded, waiting before rendering PR Age chart...');
            await new Promise(resolve => setTimeout(resolve, 500));
            if (typeof Chart === 'undefined') {
                console.error('Chart.js failed to load, cannot render PR Age chart.');
                return;
            }
        }

        const chartElement = await window.waitForElement('prAgeChart');
        if (window.committerCharts['prAgeChart']) {
            window.committerCharts['prAgeChart'].destroy();
        }

        // Apply client-side filtering based on minimum days
        const openPrData = chartData.openPrAge
            .filter(d => d.days >= filterDays)
            .map(d => ({ x: d.days, y: d.prCount }));
        const mergedPrData = chartData.mergedPrAge
            .filter(d => d.days >= filterDays)
            .map(d => ({ x: d.days, y: d.prCount }));

        const ctx = chartElement.getContext('2d');
        window.committerCharts['prAgeChart'] = new Chart(ctx, {
            type: 'bar',
            data: {
                datasets: [
                    {
                        label: 'Open PRs',
                        data: openPrData,
                        backgroundColor: 'rgba(239, 68, 68, 0.6)',
                        borderColor: 'rgba(239, 68, 68, 1)',
                        borderWidth: 1
                    },
                    {
                        label: 'Merged PRs',
                        data: mergedPrData,
                        backgroundColor: 'rgba(34, 197, 94, 0.6)',
                        borderColor: 'rgba(34, 197, 94, 1)',
                        borderWidth: 1
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    x: {
                        type: 'linear',
                        position: 'bottom',
                        title: {
                            display: true,
                            text: 'Age in Days'
                        }
                    },
                    y: {
                        beginAtZero: true,
                        title: {
                            display: true,
                            text: 'Number of PRs'
                        }
                    }
                },
                plugins: {
                    tooltip: {
                        callbacks: {
                            title: (context) => `Age: ${context[0].raw.x} days`,
                            label: (context) => `${context.dataset.label}: ${context.raw.y} PRs`,
                            footer: (context) => filterDays > 0 ? `Filter: ≥ ${filterDays} days` : ''
                        }
                    }
                }
            }
        });
    } catch (error) {
        console.error('Error rendering PR Age chart:', error);
    }
}; 