/**
 * Dashboard JavaScript for WebVFD IPS
 * Handles chart rendering and dashboard data updates
 * This file provides support functions for the dashboard but 
 * should not directly initialize charts.
 */

// Initialize charts function (will be called from site.js or directly from Index.cshtml)
function initializeCharts() {
    console.log("Dashboard.js: Initializing dashboard charts");
    
    // Define initDashboardCharts as an alias for backwards compatibility
    if (typeof window.initDashboardCharts !== 'function') {
        window.initDashboardCharts = initializeCharts;
    }
    
    // Monthly Sales Chart
    if (document.getElementById('monthlySalesChart')) {
        const ctx = document.getElementById('monthlySalesChart').getContext('2d');
        const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        
        // Ensure no negative values in the data
        const sanitizedSales = window.monthlySales ? window.monthlySales.map(val => Math.max(0, val)) : Array(12).fill(0);
        const sanitizedTaxes = window.monthlyTaxes ? window.monthlyTaxes.map(val => Math.max(0, val)) : Array(12).fill(0);
        
        console.log("Dashboard.js Monthly chart data:", {
            sales: sanitizedSales,
            taxes: sanitizedTaxes
        });
        
        // Clear any existing chart
        if (window.monthlySalesChart) {
            window.monthlySalesChart.destroy();
        }
        
        window.monthlySalesChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: monthNames,
                datasets: [
                    {
                        label: 'Sales',
                        data: sanitizedSales,
                        backgroundColor: 'rgba(55, 125, 255, 0.7)',
                        borderColor: 'rgba(55, 125, 255, 1)',
                        borderWidth: 1
                    },
                    {
                        label: 'Taxes',
                        data: sanitizedTaxes,
                        backgroundColor: 'rgba(255, 193, 7, 0.7)',
                        borderColor: 'rgba(255, 193, 7, 1)',
                        borderWidth: 1
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function(value) {
                                return value.toLocaleString();
                            }
                        }
                    }
                },
                plugins: {
                    tooltip: {
                        callbacks: {
                            label: function(context) {
                                let label = context.dataset.label || '';
                                if (label) {
                                    label += ': ';
                                }
                                label += parseFloat(context.raw).toLocaleString('en-US', {
                                    minimumFractionDigits: 2,
                                    maximumFractionDigits: 2
                                });
                                return label;
                            }
                        }
                    }
                }
            }
        });
    }
    
    // Receipt Status Chart
    if (document.getElementById('receiptStatusChart')) {
        const ctx = document.getElementById('receiptStatusChart').getContext('2d');
        
        // Clear any existing chart
        if (window.receiptStatusChart) {
            window.receiptStatusChart.destroy();
        }
        
        window.receiptStatusChart = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['Validated Receipts', 'Failed Receipts'],
                datasets: [{
                    data: [
                        window.allReceipts || 0, 
                        window.allFailures || 0
                    ],
                    backgroundColor: [
                        'rgba(40, 167, 69, 0.8)',  // Success - green
                        'rgba(220, 53, 69, 0.8)'   // Danger - red
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
                    },
                    tooltip: {
                        callbacks: {
                            label: function(context) {
                                const dataset = context.dataset;
                                const total = dataset.data.reduce((acc, current) => acc + current, 0);
                                const currentValue = dataset.data[context.dataIndex];
                                const percentage = total > 0 ? Math.round((currentValue / total) * 100) : 0;
                                
                                return context.label + ': ' + 
                                    currentValue.toLocaleString() + ' (' + percentage + '%)';
                            }
                        }
                    }
                }
            }
        });
    }
    
    // Daily Receipts Chart
    if (document.getElementById('dailyReceiptsChart')) {
        const ctx = document.getElementById('dailyReceiptsChart').getContext('2d');
        
        console.log("Dashboard.js Daily receipts chart data:", {
            labels: window.dailyLabels || [],
            data: window.dailyReceipts || []
        });
        
        // Clear any existing chart
        if (window.dailyReceiptsChart) {
            window.dailyReceiptsChart.destroy();
        }
        
        window.dailyReceiptsChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: window.dailyLabels || [],
                datasets: [{
                    label: 'Receipts',
                    data: window.dailyReceipts || [],
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
    }
}

/**
 * Refresh dashboard data via AJAX
 */
function refreshDashboard() {
    // Show loading indicator on the refresh button
    const refreshBtn = $('#refresh-dashboard');
    const originalContent = refreshBtn.html();
    refreshBtn.html('<i class="fa fa-spinner fa-spin"></i>');
    refreshBtn.prop('disabled', true);
    
    // Fetch updated data from the server
    $.ajax({
        url: '/Home/GetDashboardData',
        method: 'GET',
        success: function(data) {
            console.log("Dashboard.js: Dashboard data refreshed:", data);
            
            // Update global variables
            window.dailyLabels = data.dailyLabels;
            window.dailyReceipts = data.dailyReceipts;
            window.monthlySales = data.monthlySales;
            window.monthlyTaxes = data.monthlyTaxes;
            window.allReceipts = data.allReceipts;
            window.allFailures = data.allFailures;
            window.allTaxes = data.allTaxes;
            window.allSales = data.allSales;
            window.monthTaxes = data.monthTaxes;
            window.monthSales = data.monthSales;
            
            // Use the updateDashboardData function if available, otherwise update directly
            if (typeof window.updateDashboardData === 'function') {
                window.updateDashboardData(data);
            } else {
                updateCharts(data);
            }
        },
        error: function(xhr, status, error) {
            console.error('Dashboard.js: Failed to refresh dashboard data:', error);
        },
        complete: function() {
            // Restore refresh button
            setTimeout(function() {
                refreshBtn.html(originalContent);
                refreshBtn.prop('disabled', false);
            }, 500);
        }
    });
}

/**
 * Update dashboard statistics with new data
 */
function updateDashboardData(data) {
    // Update statistics cards
    updateStatCard('today-sales', data.todaySales);
    updateStatCard('today-taxes', data.todayTaxes);
    updateStatCard('month-sales', data.monthSales);
    updateStatCard('month-taxes', data.monthTaxes);
    updateStatCard('all-sales', data.allSales);
    updateStatCard('all-taxes', data.allTaxes);
    updateStatCard('today-receipts', data.todayReceipts);
    updateStatCard('today-failures', data.todayFailures);
    updateStatCard('today-pending', data.todayPending);
    updateStatCard('all-receipts', data.allReceipts);
    updateStatCard('all-failures', data.allFailures);
    updateStatCard('all-pending', data.allPending);
    updateStatCard('for-save-count', data.forSaveCount);
    updateStatCard('not-for-save-count', data.notForSaveCount);
    updateStatCard('for-save-sales', data.forSaveSales);
    updateStatCard('not-for-save-sales', data.notForSaveSales);
    
    if (document.getElementById('business-count')) {
        updateStatCard('business-count', data.businessCount);
    }
    
    // Update recent invoices table if present
    if (data.invoices && data.invoices.length > 0) {
        updateRecentInvoices(data.invoices);
    }
    
    // Update charts with new data
    updateCharts(data);
}

/**
 * Update a stat card with new value with animation
 */
function updateStatCard(id, value) {
    const element = document.getElementById(id);
    if (element) {
        // Format the value
        const formattedValue = typeof value === 'number' 
            ? value.toLocaleString('en-US', {
                minimumFractionDigits: id.includes('sales') || id.includes('taxes') ? 2 : 0,
                maximumFractionDigits: id.includes('sales') || id.includes('taxes') ? 2 : 0
              })
            : value;
        
        // Animate value change
        $(element).fadeOut(200, function() {
            $(this).text(formattedValue).fadeIn(200);
        });
    }
}

/**
 * Update recent invoices table with new data
 */
function updateRecentInvoices(invoices) {
    const container = document.querySelector('table.table tbody');
    if (container && invoices && invoices.length > 0) {
        let html = '';
        
        invoices.forEach(invoice => {
            const date = new Date(invoice.date);
            const formattedDate = `${date.getFullYear()}-${(date.getMonth() + 1).toString().padStart(2, '0')}-${date.getDate().toString().padStart(2, '0')} ${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}`;
            const status = invoice.status === 'VALIDATED' 
                ? '<span class="badge bg-success">VALIDATED</span>'
                : `<span class="badge bg-danger">${invoice.status}</span>`;
                
            const total = invoice.total !== undefined ? invoice.total : 
                          (invoice.items && invoice.items.length > 0 ? 
                          invoice.items.reduce((sum, item) => sum + item.amount + item.tax, 0) : 0);
                
            html += `
                <tr>
                    <td>${invoice.receiptNumber}</td>
                    <td>${formattedDate}</td>
                    <td>${total.toLocaleString('en-US', {minimumFractionDigits: 2, maximumFractionDigits: 2})}</td>
                    <td>${status}</td>
                    <td>
                        <a href="/Invoices/Details/${invoice.id}" class="btn btn-sm btn-light" data-link="Invoices/Details/${invoice.id}">
                            <i class="fa fa-print"></i> Print
                        </a>
                    </td>
                </tr>
            `;
        });
        
        // Update table with fade effect
        $(container).fadeOut(200, function() {
            container.innerHTML = html;
            $(container).fadeIn(200);
        });
    }
}

/**
 * Update charts with new data
 */
function updateCharts(data) {
    // Update global variables to ensure all chart code is using the same data
    window.dailyLabels = data.dailyLabels;
    window.dailyReceipts = data.dailyReceipts;
    window.monthlySales = data.monthlySales;
    window.monthlyTaxes = data.monthlyTaxes;
    window.allReceipts = data.allReceipts;
    window.allFailures = data.allFailures;
    
    // Sanitize data to avoid negative values
    const sanitizedMonthlySales = window.monthlySales ? 
        window.monthlySales.map(val => Math.max(0, val)) : Array(12).fill(0);
        
    const sanitizedMonthlyTaxes = window.monthlyTaxes ? 
        window.monthlyTaxes.map(val => Math.max(0, val)) : Array(12).fill(0);
    
    // Update monthly sales chart
    if (window.monthlySalesChart) {
        console.log("Dashboard.js: Updating monthly sales chart");
        window.monthlySalesChart.data.datasets[0].data = sanitizedMonthlySales;
        window.monthlySalesChart.data.datasets[1].data = sanitizedMonthlyTaxes;
        window.monthlySalesChart.update();
    }
    
    // Update daily receipts chart
    if (window.dailyReceiptsChart && window.dailyLabels && window.dailyReceipts) {
        console.log("Dashboard.js: Updating daily receipts chart");
        window.dailyReceiptsChart.data.labels = window.dailyLabels;
        window.dailyReceiptsChart.data.datasets[0].data = window.dailyReceipts;
        window.dailyReceiptsChart.update();
    }
    
    // Update receipt status chart
    if (window.receiptStatusChart) {
        console.log("Dashboard.js: Updating receipt status chart");
        window.receiptStatusChart.data.datasets[0].data = [
            window.allReceipts || 0,
            window.allFailures || 0
        ];
        window.receiptStatusChart.update();
    }
} 