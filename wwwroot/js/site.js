// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

/**
 * Dashboard data update function
 */
function updateDashboardData(data) {
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
    
    // Update statistics
    $('#today-sales').text(formatNumber(data.todaySales));
    $('#today-taxes').text(formatNumber(data.todayTaxes));
    $('#month-sales').text(formatNumber(data.monthSales));
    $('#month-taxes').text(formatNumber(data.monthTaxes));
    $('#all-sales').text(formatNumber(data.allSales));
    $('#all-taxes').text(formatNumber(data.allTaxes));
    $('#today-receipts').text(formatNumber(data.todayReceipts));
    $('#all-receipts').text(formatNumber(data.allReceipts));
    $('#today-failures').text(formatNumber(data.todayFailures));
    $('#all-failures').text(formatNumber(data.allFailures));
    $('#today-pending').text(formatNumber(data.todayPending));
    $('#all-pending').text(formatNumber(data.allPending));
    $('#business-count').text(formatNumber(data.businessCount));
    
    // Update ForSave statistics
    $('#for-save-count').text(formatNumber(data.forSaveCount));
    $('#not-for-save-count').text(formatNumber(data.notForSaveCount));
    $('#for-save-sales').text(formatNumber(data.forSaveSales));
    $('#not-for-save-sales').text(formatNumber(data.notForSaveSales));
    
    // Calculate and update percentages
    if (data.forSaveCount + data.notForSaveCount > 0) {
        const forSavePercent = ((data.forSaveCount / (data.forSaveCount + data.notForSaveCount)) * 100).toFixed(1);
        const notForSavePercent = ((data.notForSaveCount / (data.forSaveCount + data.notForSaveCount)) * 100).toFixed(1);
        $('#for-save-count').siblings('.text-success').find('span').text(forSavePercent + '%');
        $('#not-for-save-count').siblings('.text-danger').find('span').text(notForSavePercent + '%');
    }
    
    if (data.forSaveSales + data.notForSaveSales > 0) {
        const forSaveSalesPercent = ((data.forSaveSales / (data.forSaveSales + data.notForSaveSales)) * 100).toFixed(1);
        const notForSaveSalesPercent = ((data.notForSaveSales / (data.forSaveSales + data.notForSaveSales)) * 100).toFixed(1);
        $('#for-save-sales').siblings('.text-primary').find('span').text(forSaveSalesPercent + '%');
        $('#not-for-save-sales').siblings('.text-warning').find('span').text(notForSaveSalesPercent + '%');
    }
    
    // Update charts if they exist
    if (typeof updateCharts === 'function') {
        updateCharts(data);
    } else {
        // Fallback chart updates
        updateDailyReceiptsChart(data.dailyLabels, data.dailyReceipts);
        updateMonthlySalesChart(data.monthlySales, data.monthlyTaxes);
        updateReceiptStatusChart(data.allReceipts, data.allFailures);
    }
}

function formatNumber(num) {
    if (num === undefined || num === null) return "0";
    return parseFloat(num).toLocaleString('en-US', {
        minimumFractionDigits: typeof num === 'number' && Number.isInteger(num) ? 0 : 2,
        maximumFractionDigits: 2
    });
}

function updateDailyReceiptsChart(labels, data) {
    if (!window.dailyReceiptsChart) return;
    
    window.dailyReceiptsChart.data.labels = labels;
    window.dailyReceiptsChart.data.datasets[0].data = data;
    window.dailyReceiptsChart.update();
}

function updateMonthlySalesChart(salesData, taxData) {
    if (!window.monthlySalesChart) return;
    
    // Sanitize data to avoid negative values
    const sanitizedSales = salesData ? salesData.map(val => Math.max(0, val)) : Array(12).fill(0);
    const sanitizedTaxes = taxData ? taxData.map(val => Math.max(0, val)) : Array(12).fill(0);
    
    window.monthlySalesChart.data.datasets[0].data = sanitizedSales;
    window.monthlySalesChart.data.datasets[1].data = sanitizedTaxes;
    window.monthlySalesChart.update();
}

function updateReceiptStatusChart(receipts, failures) {
    if (!window.receiptStatusChart) return;
    
    window.receiptStatusChart.data.datasets[0].data = [receipts || 0, failures || 0];
    window.receiptStatusChart.update();
}

/**
 * Initialize dashboard charts
 */
function initDashboardCharts() {
    // Daily receipts chart
    const dailyCtx = document.getElementById('dailyReceiptsChart');
    if (dailyCtx) {
        console.log("Initializing daily receipts chart");
        
        // Default empty data
        const dailyLabels = window.dailyLabels || [];
        const dailyReceipts = window.dailyReceipts || [];
        
        if (window.dailyReceiptsChart) {
            window.dailyReceiptsChart.destroy();
        }
        
        window.dailyReceiptsChart = new Chart(dailyCtx, {
            type: 'bar',
            data: {
                labels: dailyLabels,
                datasets: [{
                    label: 'Receipts',
                    data: dailyReceipts,
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
                },
                animation: {
                    duration: 500
                }
            }
        });
    }
    
    // Monthly sales chart
    const monthlyCtx = document.getElementById('monthlySalesChart');
    if (monthlyCtx) {
        console.log("Initializing monthly sales chart");
        const monthLabels = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        
        // Ensure data is valid, use empty arrays if not
        const sanitizedSales = window.monthlySales ? window.monthlySales.map(val => Math.max(0, val)) : Array(12).fill(0);
        const sanitizedTaxes = window.monthlyTaxes ? window.monthlyTaxes.map(val => Math.max(0, val)) : Array(12).fill(0);
        
        if (window.monthlySalesChart) {
            window.monthlySalesChart.destroy();
        }
        
        window.monthlySalesChart = new Chart(monthlyCtx, {
            type: 'bar',
            data: {
                labels: monthLabels,
                datasets: [
                    {
                        label: 'Sales',
                        data: sanitizedSales,
                        backgroundColor: 'rgba(54, 162, 235, 0.7)',
                        borderColor: 'rgba(54, 162, 235, 1)',
                        borderWidth: 1
                    },
                    {
                        label: 'Taxes',
                        data: sanitizedTaxes,
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
                        beginAtZero: true,
                        ticks: {
                            callback: function(value) {
                                return value.toLocaleString();
                            }
                        }
                    }
                },
                animation: {
                    duration: 500
                }
            }
        });
    }
    
    // Receipt Status Chart
    const statusCtx = document.getElementById('receiptStatusChart');
    if (statusCtx) {
        console.log("Initializing receipt status chart");
        
        if (window.receiptStatusChart) {
            window.receiptStatusChart.destroy();
        }
        
        window.receiptStatusChart = new Chart(statusCtx, {
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
                },
                animation: {
                    duration: 500
                }
            }
        });
    }
}

/**
 * WebVFD IPS - Common JavaScript Functions
 */
$(document).ready(function() {
    // Setup AJAX form submissions
    setupAjaxForms();
    
    // Setup dashboard refresh if on dashboard page
    if ($('#dashboard-container').length) {
        setupDashboardRefresh();
    }
    
    // Initialize any dataTables
    initializeDataTables();
});

/**
 * Setup AJAX form submission handling
 */
function setupAjaxForms() {
    // Handle AJAX form submissions with .ajax-form class
    $(document).on('submit', '.ajax-form', function(e) {
        e.preventDefault();
        const form = $(this);
        const submitBtn = form.find('[type="submit"]');
        const originalBtnText = submitBtn.html();
        
        // Show loading indicator
        submitBtn.html('<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Processing...');
        submitBtn.prop('disabled', true);
        
        $.ajax({
            url: form.attr('action'),
            type: form.attr('method') || 'POST',
            data: new FormData(form[0]),
            processData: false,
            contentType: false,
            success: function(response) {
                // Check if response has a redirect URL
                if (response.redirectUrl) {
                    window.location.href = response.redirectUrl;
                    return;
                }
                
                // Check if response has a message
                if (response.message) {
                    showToast('Success', response.message, 'success');
                }
                
                // Check if response has HTML to update a target
                if (response.updateTarget && response.html) {
                    $(response.updateTarget).html(response.html);
                }
                
                // Reset form if needed
                if (response.resetForm) {
                    form[0].reset();
                }
                
                // Trigger custom event for additional processing
                $(document).trigger('ajaxFormSuccess', [response, form]);
            },
            error: function(xhr) {
                let errorMessage = 'An error occurred while processing your request.';
                
                if (xhr.responseJSON && xhr.responseJSON.message) {
                    errorMessage = xhr.responseJSON.message;
                }
                
                showToast('Error', errorMessage, 'error');
                
                // Trigger custom event for additional error handling
                $(document).trigger('ajaxFormError', [xhr, form]);
            },
            complete: function() {
                // Restore button state
                submitBtn.html(originalBtnText);
                submitBtn.prop('disabled', false);
            }
        });
    });
}

/**
 * Setup dashboard refresh functionality
 */
function setupDashboardRefresh() {
    // Add refresh button click handler
    $('#refresh-dashboard').on('click', function(e) {
        e.preventDefault();
        refreshDashboard();
    });
    
    // Set interval for automatic refresh
    setInterval(refreshDashboard, 60000); // Refresh every 60 seconds
}

/**
 * Refresh dashboard data via AJAX
 */
function refreshDashboard() {
    const dashboardContainer = $('#dashboard-container');
    
    // Show loading indicator
    const loadingIndicator = $('<div class="position-absolute top-0 start-0 w-100 h-100 d-flex justify-content-center align-items-center bg-white bg-opacity-75"><div class="spinner-border text-primary" role="status"></div></div>');
    dashboardContainer.css('position', 'relative').append(loadingIndicator);
    
    $.ajax({
        url: '/Home/GetDashboardData',
        type: 'GET',
        headers: {
            'X-Requested-With': 'XMLHttpRequest'
        },
        success: function(data) {
            updateDashboardData(data);
        },
        error: function(xhr) {
            console.error('Dashboard refresh error:', xhr);
            showToast('Error', 'Failed to refresh dashboard data', 'error');
        },
        complete: function() {
            loadingIndicator.remove();
        }
    });
}

/**
 * Update dashboard with fresh data
 */
function updateDashboardData(data) {
    // Update dashboard cards
    $('#today-sales').text(formatCurrency(data.todaySales));
    $('#today-taxes').text(formatCurrency(data.todayTaxes));
    $('#month-sales').text(formatCurrency(data.monthSales));
    $('#month-taxes').text(formatCurrency(data.monthTaxes));
    $('#all-sales').text(formatCurrency(data.allSales));
    $('#all-taxes').text(formatCurrency(data.allTaxes));
    
    // Update receipt counts if elements exist
    if ($('#today-receipts').length) $('#today-receipts').text(data.todayReceipts);
    if ($('#all-receipts').length) $('#all-receipts').text(data.allReceipts);
    if ($('#today-failures').length) $('#today-failures').text(data.todayFailures);
    if ($('#all-failures').length) $('#all-failures').text(data.allFailures);
    if ($('#today-pending').length) $('#today-pending').text(data.todayPending);
    if ($('#all-pending').length) $('#all-pending').text(data.allPending);
    
    // Update invoice list if present
    if ($('#latest-invoices').length && data.invoices && data.invoices.length > 0) {
        const list = $('#latest-invoices');
        list.empty();
        
        data.invoices.forEach(function(invoice) {
            const date = new Date(invoice.date).toLocaleDateString();
            const item = `
                <div class="card mb-2">
                    <div class="card-body py-2">
                        <div class="d-flex align-items-center">
                            <div>
                                <h6 class="mb-0">${invoice.invoiceNo || 'N/A'}</h6>
                                <small class="text-muted">${date}</small>
                            </div>
                            <div class="ms-auto">
                                <h6 class="mb-0">${formatCurrency(invoice.total)}</h6>
                                <small class="text-muted">Tax: ${formatCurrency(invoice.tax)}</small>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            list.append(item);
        });
    }
    
    // Update charts if they exist
    updateDashboardCharts(data);
}

/**
 * Update dashboard charts with fresh data
 */
function updateDashboardCharts(data) {
    // Check if chart objects exist in window
    if (window.dailyReceiptsChart) {
        window.dailyReceiptsChart.data.labels = data.dailyLabels;
        window.dailyReceiptsChart.data.datasets[0].data = data.dailyReceipts;
        window.dailyReceiptsChart.update();
    }
    
    if (window.monthlySalesChart) {
        window.monthlySalesChart.data.datasets[0].data = data.monthlySales;
        window.monthlySalesChart.data.datasets[1].data = data.monthlyTaxes;
        window.monthlySalesChart.update();
    }
    
    if (window.receiptStatusChart && data.allReceipts && data.allFailures) {
        window.receiptStatusChart.data.datasets[0].data = [
            data.allReceipts,
            data.allFailures,
            data.allPending || 0
        ];
        window.receiptStatusChart.update();
    }
}

/**
 * Initialize DataTables
 */
function initializeDataTables() {
    if ($.fn.DataTable) {
        $('.datatable').each(function() {
            const table = $(this);
            if (!$.fn.DataTable.isDataTable(table)) {
                table.DataTable({
                    responsive: true,
                    language: {
                        search: "",
                        searchPlaceholder: "Search..."
                    }
                });
            }
        });
    }
}

/**
 * Show a toast message
 */
function showToast(title, message, type = 'info') {
    // Check if toastr library is available
    if (typeof toastr !== 'undefined') {
        toastr[type](message, title);
        return;
    }
    
    // Fallback to bootstrap toast if available
    const toastHtml = `
        <div class="toast align-items-center text-white bg-${type === 'error' ? 'danger' : type}" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="d-flex">
                <div class="toast-body">
                    <strong>${title}:</strong> ${message}
                </div>
                <button type="button" class="btn-close me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        </div>
    `;
    
    const toastContainer = $('.toast-container');
    if (toastContainer.length === 0) {
        $('body').append('<div class="toast-container position-fixed top-0 end-0 p-3"></div>');
    }
    
    const toast = $(toastHtml);
    $('.toast-container').append(toast);
    
    // Initialize and show toast if Bootstrap is available
    if (typeof bootstrap !== 'undefined') {
        const bsToast = new bootstrap.Toast(toast);
        bsToast.show();
    }
}

/**
 * Format a number as currency
 */
function formatCurrency(value) {
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'TZS',
        minimumFractionDigits: 2
    }).format(value);
}
