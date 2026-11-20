/**
 * Dashboard Charts
 * Handles chart initialization and updates for the dashboard page
 */

// Global chart objects
window.dailyReceiptsChart = null;
window.monthlySalesChart = null;
window.receiptStatusChart = null;

/**
 * Initialize all dashboard charts with empty data
 */
function initDashboardCharts() {
    console.log("Initializing dashboard charts...");
    
    // Check if Chart.js is loaded
    if (typeof Chart === 'undefined') {
        console.error("Chart.js is not loaded! Charts will not work.");
        return;
    } else {
        console.log("Chart.js is loaded properly.");
    }
    
    // Check if chart canvases exist
    const dailyCanvasExists = document.getElementById('dailyReceiptsChart') !== null;
    const monthlyCanvasExists = document.getElementById('monthlySalesChart') !== null;
    const statusCanvasExists = document.getElementById('receiptStatusChart') !== null;
    
    console.log("Chart canvases:", {
        daily: dailyCanvasExists,
        monthly: monthlyCanvasExists,
        status: statusCanvasExists
    });
    
    if (!statusCanvasExists) {
        console.error("Receipt status chart canvas not found! Check the HTML element with id 'receiptStatusChart'");
    }
    
    // Daily receipts chart
    const dailyCtx = document.getElementById('dailyReceiptsChart');
    if (dailyCtx) {
        try {
            window.dailyReceiptsChart = new Chart(dailyCtx, {
                type: 'bar',
                data: {
                    labels: [],
                    datasets: [{
                        label: 'Receipts',
                        data: [],
                        backgroundColor: 'rgba(40, 167, 69, 0.7)',
                        borderColor: 'rgba(40, 167, 69, 1)',
                        borderWidth: 1
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: {
                                precision: 0
                            }
                        }
                    }
                }
            });
            console.log("Daily receipts chart initialized successfully");
        } catch (e) {
            console.error("Error initializing daily receipts chart:", e);
        }
    }
    
    // Monthly sales chart
    const monthlyCtx = document.getElementById('monthlySalesChart');
    if (monthlyCtx) {
        try {
            const monthLabels = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
            
            window.monthlySalesChart = new Chart(monthlyCtx, {
                type: 'bar',
                data: {
                    labels: monthLabels,
                    datasets: [
                        {
                            label: 'Sales',
                            data: Array(12).fill(0),
                            backgroundColor: 'rgba(54, 162, 235, 0.7)',
                            borderColor: 'rgba(54, 162, 235, 1)',
                            borderWidth: 1
                        },
                        {
                            label: 'Taxes',
                            data: Array(12).fill(0),
                            backgroundColor: 'rgba(255, 99, 132, 0.7)',
                            borderColor: 'rgba(255, 99, 132, 1)',
                            borderWidth: 1
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: {
                        y: {
                            beginAtZero: true
                        }
                    }
                }
            });
            console.log("Monthly sales chart initialized successfully");
        } catch (e) {
            console.error("Error initializing monthly sales chart:", e);
        }
    }
    
    // Receipt status chart
    const statusCtx = document.getElementById('receiptStatusChart');
    if (statusCtx) {
        try {
            console.log("Initializing receipt status chart...");
            window.receiptStatusChart = new Chart(statusCtx, {
                type: 'doughnut',
                data: {
                    labels: ['Validated Receipts', 'Failed Receipts'],
                    datasets: [{
                        data: [1, 1], // Start with non-zero data to ensure the chart is visible
                        backgroundColor: [
                            'rgba(40, 167, 69, 0.8)',
                            'rgba(220, 53, 69, 0.8)'
                        ],
                        borderWidth: 1
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            position: 'bottom'
                        }
                    }
                }
            });
            console.log("Receipt status chart initialized successfully");
        } catch (e) {
            console.error("Error initializing receipt status chart:", e);
        }
    }
}

/**
 * Update dashboard charts with new data
 * @param {Object} data - The dashboard data from the server
 */
function updateDashboardCharts(data) {
    console.log("Updating charts with new data");
    
    if (window.dailyReceiptsChart) {
        console.log("Updating daily receipts chart");
        window.dailyReceiptsChart.data.labels = data.dailyLabels;
        window.dailyReceiptsChart.data.datasets[0].data = data.dailyReceipts;
        window.dailyReceiptsChart.update();
    } else {
        console.warn("Daily receipts chart not initialized!");
    }
    
    if (window.monthlySalesChart) {
        console.log("Updating monthly sales chart");
        window.monthlySalesChart.data.datasets[0].data = data.monthlySales.map(val => Math.max(0, val));
        window.monthlySalesChart.data.datasets[1].data = data.monthlyTaxes.map(val => Math.max(0, val));
        window.monthlySalesChart.update();
    } else {
        console.warn("Monthly sales chart not initialized!");
    }
    
    if (window.receiptStatusChart) {
        console.log("Updating receipt status chart with data:", {
            receipts: data.allReceipts, 
            failures: data.allFailures
        });
        
        // Make sure we don't have zero for both values as that can cause rendering issues
        const receipts = Math.max(data.allReceipts, 1);
        const failures = Math.max(data.allFailures, 1);
        
        window.receiptStatusChart.data.datasets[0].data = [receipts, failures];
        window.receiptStatusChart.update();
        console.log("Receipt status chart updated successfully");
    } else {
        console.warn("Receipt status chart not initialized!");
    }
} 