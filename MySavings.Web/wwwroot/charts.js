// Chart.js rendering functions
window.renderCharts = function(months, salaries, expenses, savings, ratios, cumulativeSavings) {
    console.log('Rendering charts with data:', { months, salaries, expenses, savings, ratios, cumulativeSavings });
    window._lastChartArgs = [months, salaries, expenses, savings, ratios, cumulativeSavings];

    // Destroy existing charts if they exist
    if (window.salaryChartInstance) window.salaryChartInstance.destroy();
    if (window.expensesChartInstance) window.expensesChartInstance.destroy();
    if (window.savingsChartInstance) window.savingsChartInstance.destroy();
    if (window.ratioChartInstance) window.ratioChartInstance.destroy();
    if (window.cumulativeSavingsChartInstance) window.cumulativeSavingsChartInstance.destroy();

    if (!months || months.length === 0) {
        console.log('No visible month. Charts cleared.');
        return;
    }

    // Function to calculate average
    function calculateAverage(data) {
        if (!data || data.length === 0) {
            return 0;
        }

        const sum = data.reduce((acc, val) => acc + val, 0);
        return sum / data.length;
    }

    // Calculate averages
    const avgSalary = calculateAverage(salaries);
    const avgExpenses = calculateAverage(expenses);
    const avgSavings = calculateAverage(savings);
    const avgRatio = calculateAverage(ratios);

    // Function to calculate linear regression trend metrics
    function calculateTrendMetrics(data) {
        const n = data.length;

        if (n === 0) {
            return { trendLine: [], slope: 0, intercept: 0, pointsCount: 0 };
        }

        if (n === 1) {
            return { trendLine: [data[0]], slope: 0, intercept: data[0], pointsCount: 1 };
        }

        let sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

        for (let i = 0; i < n; i++) {
            sumX += i;
            sumY += data[i];
            sumXY += i * data[i];
            sumX2 += i * i;
        }

        const denominator = (n * sumX2 - sumX * sumX);
        if (denominator === 0) {
            return {
                trendLine: [...data],
                slope: 0,
                intercept: data[0],
                pointsCount: n
            };
        }

        const slope = (n * sumXY - sumX * sumY) / denominator;
        const intercept = (sumY - slope * sumX) / n;

        return {
            trendLine: data.map((_, i) => slope * i + intercept),
            slope,
            intercept,
            pointsCount: n
        };
    }

    function getEvolutionDisplay(trendMetrics, fallbackAverage, isInverseLogic = false) {
        if (!trendMetrics || trendMetrics.pointsCount < 2) {
            return { value: 0, arrow: '→', color: '#6b7280' };
        }

        const trendDelta = trendMetrics.slope * (trendMetrics.pointsCount - 1);
        const baseline = Math.abs(trendMetrics.intercept) > 0.0001
            ? Math.abs(trendMetrics.intercept)
            : Math.abs(fallbackAverage);

        let changePercent = 0;
        if (baseline < 0.0001) {
            changePercent = Math.abs(trendDelta) < 0.0001 ? 0 : 100;
        } else {
            changePercent = (trendDelta / baseline) * 100;
        }

        if (Math.abs(changePercent) < 0.0001) {
            return { value: 0, arrow: '→', color: '#6b7280' };
        }

        const isPositive = changePercent > 0;
        const arrow = isPositive ? '↗' : '↘';
        const positiveColor = isInverseLogic ? '#dc2626' : '#16a34a';
        const negativeColor = isInverseLogic ? '#16a34a' : '#dc2626';

        return {
            value: changePercent,
            arrow,
            color: isPositive ? positiveColor : negativeColor
        };
    }

    // Calculate trend lines and evolution from trend coefficients
    const salaryTrendMetrics = calculateTrendMetrics(salaries);
    const expensesTrendMetrics = calculateTrendMetrics(expenses);
    const savingsTrendMetrics = calculateTrendMetrics(savings);
    const ratioTrendMetrics = calculateTrendMetrics(ratios);

    const salaryTrend = salaryTrendMetrics.trendLine;
    const expensesTrend = expensesTrendMetrics.trendLine;
    const savingsTrend = savingsTrendMetrics.trendLine;
    const ratioTrend = ratioTrendMetrics.trendLine;

    const salaryEvolution = getEvolutionDisplay(salaryTrendMetrics, avgSalary);
    const expensesEvolution = getEvolutionDisplay(expensesTrendMetrics, avgExpenses, true);
    const savingsEvolution = getEvolutionDisplay(savingsTrendMetrics, avgSavings);
    const ratioEvolution = getEvolutionDisplay(ratioTrendMetrics, avgRatio);

    // Plugin to display average in the left padding area (outside the plot area)
    const averagePlugin = {
        id: 'averageDisplay',
        afterDraw: (chart) => {
            const ctx = chart.ctx;
            const chartArea = chart.chartArea;
            const average = chart.options.plugins.averageValue;

            if (average !== undefined) {
                ctx.save();
                ctx.font = 'bold 11px Arial';
                ctx.fillStyle = '#888';
                ctx.textAlign = 'left';
                ctx.textBaseline = 'middle';

                const text = `Moy:\n${average.toFixed(0)}`;
                const x = 4;
                const y = chartArea.top + (chartArea.bottom - chartArea.top) / 2;

                const lines = text.split('\n');
                const lineHeight = 13;
                const startY = y - ((lines.length - 1) * lineHeight) / 2;
                lines.forEach((line, i) => {
                    ctx.fillText(line, x, startY + i * lineHeight);
                });
                ctx.restore();
            }
        }
    };

    const evolutionPlugin = {
        id: 'evolutionDisplay',
        afterDraw: (chart) => {
            const ctx = chart.ctx;
            const chartArea = chart.chartArea;
            const evolution = chart.options.plugins.evolutionValue;

            if (!evolution) {
                return;
            }

            ctx.save();
            ctx.textAlign = 'right';
            ctx.textBaseline = 'middle';

            const x = chart.width - 6;
            const y = chartArea.top + (chartArea.bottom - chartArea.top) / 2;

            ctx.font = 'bold 11px Arial';
            ctx.fillStyle = '#888';
            ctx.fillText('Évol:', x, y - 7);

            ctx.font = 'bold 12px Arial';
            ctx.fillStyle = evolution.color;
            ctx.fillText(`${evolution.arrow} ${evolution.value.toFixed(1)}%`, x, y + 7);
            ctx.restore();
        }
    };

    // Salary Chart
    const salaryCtx = document.getElementById('salaryChart');
    if (salaryCtx) {
        window.salaryChartInstance = new Chart(salaryCtx, {
            type: 'line',
            data: {
                labels: months,
                datasets: [{
                    label: 'Revenus',
                    data: salaries,
                    borderColor: '#2563eb',
                    backgroundColor: 'rgba(37, 99, 235, 0.1)',
                    fill: true,
                    tension: 0.4,
                    borderWidth: 2
                }, {
                    label: 'Tendance',
                    data: salaryTrend,
                    borderColor: '#1e40af',
                    backgroundColor: 'transparent',
                    borderDash: [5, 5],
                    borderWidth: 2,
                    pointRadius: 0,
                    fill: false,
                    tension: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                layout: {
                    padding: {
                        left: 80,
                        right: 90
                    }
                },
                plugins: {
                    legend: {
                        display: true,
                        position: 'bottom'
                    },
                    title: {
                        display: true,
                        text: 'Revenus',
                        font: {
                            size: 14,
                            weight: 'bold'
                        }
                    },
                    averageValue: avgSalary,
                    evolutionValue: salaryEvolution
                },
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            },
            plugins: [averagePlugin, evolutionPlugin]
        });
    }

    // Expenses Chart
    const expensesCtx = document.getElementById('expensesChart');
    if (expensesCtx) {
        window.expensesChartInstance = new Chart(expensesCtx, {
            type: 'line',
            data: {
                labels: months,
                datasets: [{
                    label: 'Dépenses',
                    data: expenses,
                    borderColor: '#dc2626',
                    backgroundColor: 'rgba(220, 38, 38, 0.1)',
                    fill: true,
                    tension: 0.4,
                    borderWidth: 2
                }, {
                    label: 'Tendance',
                    data: expensesTrend,
                    borderColor: '#991b1b',
                    backgroundColor: 'transparent',
                    borderDash: [5, 5],
                    borderWidth: 2,
                    pointRadius: 0,
                    fill: false,
                    tension: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                layout: {
                    padding: {
                        left: 80,
                        right: 90
                    }
                },
                plugins: {
                    legend: {
                        display: true,
                        position: 'bottom'
                    },
                    title: {
                        display: true,
                        text: 'Dépenses',
                        font: {
                            size: 14,
                            weight: 'bold'
                        }
                    },
                    averageValue: avgExpenses,
                    evolutionValue: expensesEvolution
                },
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            },
            plugins: [averagePlugin, evolutionPlugin]
        });
    }

    // Savings Chart
    const savingsCtx = document.getElementById('savingsChart');
    if (savingsCtx) {
        window.savingsChartInstance = new Chart(savingsCtx, {
            type: 'line',
            data: {
                labels: months,
                datasets: [{
                    label: 'Épargne',
                    data: savings,
                    borderColor: '#16a34a',
                    backgroundColor: 'rgba(22, 163, 74, 0.1)',
                    fill: true,
                    tension: 0.4,
                    borderWidth: 2
                }, {
                    label: 'Tendance',
                    data: savingsTrend,
                    borderColor: '#15803d',
                    backgroundColor: 'transparent',
                    borderDash: [5, 5],
                    borderWidth: 2,
                    pointRadius: 0,
                    fill: false,
                    tension: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                layout: {
                    padding: {
                        left: 80,
                        right: 90
                    }
                },
                plugins: {
                    legend: {
                        display: true,
                        position: 'bottom'
                    },
                    title: {
                        display: true,
                        text: 'Épargne',
                        font: {
                            size: 14,
                            weight: 'bold'
                        }
                    },
                    averageValue: avgSavings,
                    evolutionValue: savingsEvolution
                },
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            },
            plugins: [averagePlugin, evolutionPlugin]
        });
    }

    // Ratio Chart
    const ratioCtx = document.getElementById('ratioChart');
    if (ratioCtx) {
        window.ratioChartInstance = new Chart(ratioCtx, {
            type: 'line',
            data: {
                labels: months,
                datasets: [{
                    label: 'Ratio E/S',
                    data: ratios,
                    borderColor: '#7c3aed',
                    backgroundColor: 'rgba(124, 58, 237, 0.1)',
                    fill: true,
                    tension: 0.4,
                    borderWidth: 2
                }, {
                    label: 'Tendance',
                    data: ratioTrend,
                    borderColor: '#5b21b6',
                    backgroundColor: 'transparent',
                    borderDash: [5, 5],
                    borderWidth: 2,
                    pointRadius: 0,
                    fill: false,
                    tension: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                layout: {
                    padding: {
                        left: 80,
                        right: 90
                    }
                },
                plugins: {
                    legend: {
                        display: true,
                        position: 'bottom'
                    },
                    title: {
                        display: true,
                        text: 'Ratio E/S (%)',
                        font: {
                            size: 14,
                            weight: 'bold'
                        }
                    },
                    averageValue: avgRatio,
                    evolutionValue: ratioEvolution
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        max: 100
                    }
                }
            },
            plugins: [averagePlugin, evolutionPlugin]
        });
    }

    // Cumulative Savings Chart (running total over the displayed period)
    const cumulativeCtx = document.getElementById('cumulativeSavingsChart');
    if (cumulativeCtx && cumulativeSavings) {
        window.cumulativeSavingsChartInstance = new Chart(cumulativeCtx, {
            type: 'line',
            data: {
                labels: months,
                datasets: [{
                    label: 'Épargne cumulée',
                    data: cumulativeSavings,
                    borderColor: '#0891b2',
                    backgroundColor: 'rgba(8, 145, 178, 0.15)',
                    fill: true,
                    tension: 0.4,
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                layout: {
                    padding: {
                        left: 10,
                        right: 10
                    }
                },
                plugins: {
                    legend: {
                        display: true,
                        position: 'bottom'
                    },
                    title: {
                        display: true,
                        text: 'Épargne cumulée (période affichée)',
                        font: {
                            size: 14,
                            weight: 'bold'
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: false
                    }
                }
            }
        });
    }

    console.log('Charts rendered successfully');
};

// Liquidity breakdown: share of total allocation weight per liquidity level
window.renderLiquidityChart = function(labels, percentages) {
    console.log('Rendering liquidity chart with data:', { labels, percentages });

    if (window.liquidityChartInstance) window.liquidityChartInstance.destroy();

    const liquidityCtx = document.getElementById('liquidityChart');
    if (!liquidityCtx || !labels || labels.length === 0) {
        return;
    }

    const colorByLabel = {
        'Liquide': '#16a34a',
        'Moyen Terme': '#f59e0b',
        'Long Terme': '#dc2626'
    };
    const colors = labels.map((label) => colorByLabel[label] || '#6b7280');

    window.liquidityChartInstance = new Chart(liquidityCtx, {
        type: 'pie',
        data: {
            labels: labels,
            datasets: [{
                data: percentages,
                backgroundColor: colors,
                borderColor: '#fff',
                borderWidth: 2
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: true,
                    position: 'bottom'
                },
                tooltip: {
                    callbacks: {
                        label: (context) => `${context.label}: ${context.parsed.toFixed(2)}%`
                    }
                }
            }
        }
    });

    console.log('Liquidity chart rendered successfully');
};

// Called after switching the mobile chart tab: a chart previously hidden via
// display:none has a 0x0 canvas, so Chart.js needs an explicit resize once it becomes visible again.
window.resizeVisibleCharts = function() {
    const instances = [
        window.salaryChartInstance,
        window.expensesChartInstance,
        window.savingsChartInstance,
        window.ratioChartInstance,
        window.cumulativeSavingsChartInstance
    ];
    instances.forEach((chart) => chart && chart.resize());
};

// Called when toggling a chart's expanded (fullscreen, rotated for landscape) mode
// on mobile. Chart.js caches the container size internally (tied to its
// ResizeObserver) and does not reliably re-measure just because a CSS `transform`
// changed without the element's own untransformed layout box changing, so
// chart.resize() (with or without explicit dimensions) is a no-op here. Destroying
// and recreating all charts forces Chart.js to measure the current DOM fresh.
window.rerenderCharts = function() {
    if (window._lastChartArgs) {
        window.renderCharts(...window._lastChartArgs);
    }
};

window.downloadFileFromBase64 = function(fileName, contentType, base64Data) {
    const byteCharacters = atob(base64Data);
    const byteNumbers = new Array(byteCharacters.length);

    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }

    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: contentType });
    const url = URL.createObjectURL(blob);

    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);

    URL.revokeObjectURL(url);
};
