/**
 * WebVFD IPS - AJAX Navigation (DEACTIVATED)
 * This file is no longer in use. The application now uses traditional navigation.
 * Kept for reference purposes only.
 */

/*
$(document).ready(function() {
    // Set up the loading indicator
    const loadingIndicator = $('<div class="page-loading"></div>');
    $('body').append(loadingIndicator);

    // Track the current active link
    let currentLink = 'Home/Index';
    
    // Highlight current active link on page load
    highlightCurrentLink();

    // Handle sidebar link clicks
    $(document).on('click', '.sidebar .nav-link, [data-link]', function(e) {
        e.preventDefault();
        
        const linkPath = $(this).data('link');
        if (!linkPath) return;
        
        loadPage(linkPath);
    });

    // Function to load page content via AJAX
    function loadPage(path) {
        // Avoid reloading the same page
        if (path === currentLink) return;
        
        // Update current link
        currentLink = path;
        
        // Show loading indicator
        loadingIndicator.addClass('active');
        
        // Extract controller and action
        const parts = path.split('/');
        const controller = parts[0];
        const action = parts[1] || 'Index';
        
        // Update URL without reloading page
        window.history.pushState({ path }, '', `/${path}`);
        
        // Update active sidebar link
        highlightCurrentLink();
        
        // Load the content via AJAX
        $.ajax({
            url: `/${path}`,
            method: 'GET',
            success: function(data) {
                // Extract just the content we want to update
                const contentDiv = $('<div>').html(data);
                const mainContent = contentDiv.find('#main-content').html();
                
                // Update page title
                const pageTitle = contentDiv.find('title').text();
                document.title = pageTitle;
                
                // Update breadcrumb
                $('#current-page').text(action || controller);
                
                // Update content with animation
                $('#main-content').fadeOut(200, function() {
                    $(this).html(mainContent).fadeIn(200, function() {
                        // Initialize any scripts needed for the new content
                        initializePageScripts();
                        
                        // Trigger event for other scripts to hook into
                        $(document).trigger('ajaxNavComplete', [path]);
                    });
                });
            },
            error: function(xhr, status, error) {
                console.error('Navigation error:', error);
                
                // On error, redirect to the page normally
                window.location.href = `/${path}`;
            },
            complete: function() {
                // Hide loading indicator
                setTimeout(function() {
                    loadingIndicator.removeClass('active');
                }, 300);
            }
        });
    }
    
    // Highlight current link in sidebar
    function highlightCurrentLink() {
        // Remove active class from all links
        $('.sidebar .nav-link').removeClass('active');
        
        // Add active class to current link
        $(`.sidebar .nav-link[data-link="${currentLink}"]`).addClass('active');
    }
    
    // Initialize any scripts that need to run after page load
    function initializePageScripts() {
        // Re-initialize any DataTables
        if ($.fn.DataTable) {
            if ($.fn.DataTable.isDataTable('.datatable')) {
                $('.datatable').DataTable().destroy();
            }
            $('.datatable').DataTable({
                responsive: true
            });
        }
        
        // Re-initialize any date pickers
        if ($.fn.datepicker) {
            $('.datepicker').datepicker({
                format: 'yyyy-mm-dd',
                autoclose: true
            });
        }
        
        // Re-initialize select2 if present
        if ($.fn.select2) {
            $('.select2').select2();
        }
        
        // Check if we're on the dashboard page
        if (currentLink === 'Home/Index' || currentLink.startsWith('Home')) {
            console.log("Navigated to dashboard - initializing charts...");
            
            // Load dashboard data
            $.ajax({
                url: '/Home/GetDashboardData',
                method: 'GET',
                async: false, // Make synchronous to ensure data is available for charts
                success: function(data) {
                    console.log("Reloading dashboard data after navigation:", data);
                    
                    // Update global variables needed for charts
                    if (data) {
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
                        
                        // Wait a bit for DOM to be fully ready
                        setTimeout(function() {
                            // Make sure the charts elements exist
                            if (document.getElementById('dailyReceiptsChart') || 
                                document.getElementById('monthlySalesChart') ||
                                document.getElementById('receiptStatusChart')) {
                                    
                                console.log("Chart elements found, initializing charts...");
                                
                                // Destroy any existing charts first
                                if (window.dailyReceiptsChart) {
                                    window.dailyReceiptsChart.destroy();
                                }
                                if (window.monthlySalesChart) {
                                    window.monthlySalesChart.destroy();
                                }
                                if (window.receiptStatusChart) {
                                    window.receiptStatusChart.destroy();
                                }
                                
                                // Re-initialize dashboard charts
                                if (typeof initDashboardCharts === 'function') {
                                    initDashboardCharts();
                                } else if (typeof initializeCharts === 'function') {
                                    initializeCharts(); 
                                } else {
                                    console.error("No chart initialization function found!");
                                }
                            } else {
                                console.warn("Chart canvas elements not found in the DOM");
                            }
                        }, 200);
                    }
                },
                error: function(xhr, status, error) {
                    console.error('Failed to load dashboard data:', error);
                }
            });
        }
    }
    
    // Handle browser back/forward buttons
    window.onpopstate = function(event) {
        if (event.state && event.state.path) {
            loadPage(event.state.path);
        } else {
            // If no state, load the homepage
            loadPage('Home/Index');
        }
    };
    
    // Save initial state
    window.history.replaceState({ path: currentLink }, '', window.location.href);
    
    // Handle dashboard refresh
    $(document).on('click', '#refresh-dashboard', function(e) {
        e.preventDefault();
        refreshDashboard();
    });
    
    // Function to refresh the dashboard data via AJAX
    function refreshDashboard() {
        // Show loading indicator for the dashboard
        loadingIndicator.addClass('active');
        
        $.ajax({
            url: '/Home/GetDashboardData',
            method: 'GET',
            success: function(data) {
                if (typeof updateDashboardData === 'function') {
                    updateDashboardData(data);
                }
            },
            error: function(xhr, status, error) {
                console.error('Dashboard refresh error:', error);
            },
            complete: function() {
                // Hide loading indicator
                setTimeout(function() {
                    loadingIndicator.removeClass('active');
                }, 300);
            }
        });
    }
    
    // Set up automatic dashboard refresh
    if ($('#refresh-dashboard').length) {
        setInterval(refreshDashboard, 60000); // Refresh every 60 seconds
    }
}); 
*/ 