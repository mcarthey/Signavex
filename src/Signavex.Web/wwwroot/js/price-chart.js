// Price chart module using TradingView lightweight-charts
// Supports candlestick + line views with technical overlays and annotations

let chartInstances = {};

window.priceChart = {
    create: function (containerId, data, options) {
        this.destroy(containerId);

        const container = document.getElementById(containerId);
        if (!container) return;

        const chart = LightweightCharts.createChart(container, {
            width: container.clientWidth,
            height: options.height || 400,
            layout: {
                background: { type: 'solid', color: 'transparent' },
                textColor: '#71717a',
                fontFamily: "'Inter', sans-serif",
                fontSize: 11,
            },
            grid: {
                vertLines: { color: '#e4e4e720' },
                horzLines: { color: '#e4e4e720' },
            },
            crosshair: {
                mode: LightweightCharts.CrosshairMode.Normal,
                vertLine: { color: '#71717a40', width: 1, style: 2, labelBackgroundColor: '#4f46e5' },
                horzLine: { color: '#71717a40', width: 1, style: 2, labelBackgroundColor: '#4f46e5' },
            },
            rightPriceScale: {
                borderColor: '#e4e4e7',
                scaleMargins: { top: 0.1, bottom: 0.2 },
            },
            timeScale: {
                borderColor: '#e4e4e7',
                timeVisible: false,
                fixLeftEdge: true,
                fixRightEdge: true,
            },
            handleScroll: { vertTouchDrag: false },
        });

        const instance = { chart, series: {}, mode: options.mode || 'candlestick' };

        // Create both series types but only show the active one
        instance.series.candlestick = chart.addCandlestickSeries({
            upColor: '#16a34a',
            downColor: '#dc2626',
            borderDownColor: '#dc2626',
            borderUpColor: '#16a34a',
            wickDownColor: '#dc262680',
            wickUpColor: '#16a34a80',
            visible: instance.mode === 'candlestick',
        });

        instance.series.line = chart.addLineSeries({
            color: '#4f46e5',
            lineWidth: 2,
            crosshairMarkerRadius: 4,
            crosshairMarkerBorderColor: '#4f46e5',
            crosshairMarkerBackgroundColor: '#ffffff',
            visible: instance.mode === 'line',
        });

        // Volume histogram at the bottom
        instance.series.volume = chart.addHistogramSeries({
            priceFormat: { type: 'volume' },
            priceScaleId: 'volume',
        });
        chart.priceScale('volume').applyOptions({
            scaleMargins: { top: 0.85, bottom: 0 },
        });

        // Set OHLCV data
        if (data.ohlcv && data.ohlcv.length > 0) {
            instance.series.candlestick.setData(data.ohlcv.map(d => ({
                time: d.date,
                open: d.open,
                high: d.high,
                low: d.low,
                close: d.close,
            })));

            instance.series.line.setData(data.ohlcv.map(d => ({
                time: d.date,
                value: d.close,
            })));

            instance.series.volume.setData(data.ohlcv.map(d => ({
                time: d.date,
                value: d.volume,
                color: d.close >= d.open ? '#16a34a30' : '#dc262630',
            })));
        }

        // Moving average overlays
        if (data.sma14 && data.sma14.length > 0) {
            instance.series.sma14 = chart.addLineSeries({
                color: '#d97706',
                lineWidth: 1,
                lineStyle: 0,
                crosshairMarkerVisible: false,
                priceLineVisible: false,
                lastValueVisible: false,
                title: '14d MA',
            });
            instance.series.sma14.setData(data.sma14.map(d => ({ time: d.date, value: d.value })));
        }

        if (data.sma30 && data.sma30.length > 0) {
            instance.series.sma30 = chart.addLineSeries({
                color: '#7c3aed',
                lineWidth: 1,
                lineStyle: 0,
                crosshairMarkerVisible: false,
                priceLineVisible: false,
                lastValueVisible: false,
                title: '30d MA',
            });
            instance.series.sma30.setData(data.sma30.map(d => ({ time: d.date, value: d.value })));
        }

        // Support & resistance lines
        if (data.support != null) {
            instance.series.support = chart.addLineSeries({
                color: '#16a34a50',
                lineWidth: 1,
                lineStyle: 2,
                crosshairMarkerVisible: false,
                priceLineVisible: false,
                lastValueVisible: true,
                title: 'Support',
            });
            // Draw as horizontal line across the visible range
            const first = data.ohlcv[0].date;
            const last = data.ohlcv[data.ohlcv.length - 1].date;
            instance.series.support.setData([
                { time: first, value: data.support },
                { time: last, value: data.support },
            ]);
        }

        if (data.resistance != null) {
            instance.series.resistance = chart.addLineSeries({
                color: '#dc262650',
                lineWidth: 1,
                lineStyle: 2,
                crosshairMarkerVisible: false,
                priceLineVisible: false,
                lastValueVisible: true,
                title: 'Resistance',
            });
            const first = data.ohlcv[0].date;
            const last = data.ohlcv[data.ohlcv.length - 1].date;
            instance.series.resistance.setData([
                { time: first, value: data.resistance },
                { time: last, value: data.resistance },
            ]);
        }

        // News & event markers on the main price series
        if (data.markers && data.markers.length > 0) {
            const markers = data.markers.map(m => ({
                time: m.date,
                position: m.position || 'belowBar',
                color: m.color || '#4f46e5',
                shape: m.shape || 'circle',
                text: m.text || '',
                size: 1,
            }));

            // Markers go on whichever series is visible
            const activeSeries = instance.mode === 'candlestick'
                ? instance.series.candlestick
                : instance.series.line;
            activeSeries.setMarkers(markers);
            instance._markers = markers;
        }

        // Candlestick pattern annotations (tooltip overlay)
        this._setupTooltip(container, chart, instance, data);

        // Responsive resize
        const resizeObserver = new ResizeObserver(entries => {
            const { width } = entries[0].contentRect;
            chart.applyOptions({ width });
        });
        resizeObserver.observe(container);
        instance._resizeObserver = resizeObserver;

        chart.timeScale().fitContent();
        chartInstances[containerId] = instance;
    },

    switchMode: function (containerId, mode) {
        const instance = chartInstances[containerId];
        if (!instance) return;

        instance.mode = mode;
        instance.series.candlestick.applyOptions({ visible: mode === 'candlestick' });
        instance.series.line.applyOptions({ visible: mode === 'line' });

        // Move markers to the visible series
        if (instance._markers) {
            const activeSeries = mode === 'candlestick'
                ? instance.series.candlestick
                : instance.series.line;
            const inactiveSeries = mode === 'candlestick'
                ? instance.series.line
                : instance.series.candlestick;
            inactiveSeries.setMarkers([]);
            activeSeries.setMarkers(instance._markers);
        }
    },

    destroy: function (containerId) {
        const instance = chartInstances[containerId];
        if (instance) {
            if (instance._resizeObserver) instance._resizeObserver.disconnect();
            if (instance._tooltipEl) instance._tooltipEl.remove();
            instance.chart.remove();
            delete chartInstances[containerId];
        }
    },

    _setupTooltip: function (container, chart, instance, data) {
        // Build annotation lookup by date
        const annotations = {};
        if (data.annotations) {
            data.annotations.forEach(a => { annotations[a.date] = a; });
        }

        const tooltip = document.createElement('div');
        tooltip.className = 'chart-tooltip';
        tooltip.style.cssText = `
            position: absolute; display: none; pointer-events: none; z-index: 50;
            background: #ffffff; border: 1px solid #e4e4e7; border-radius: 8px;
            padding: 8px 12px; font-size: 11px; color: #18181b;
            box-shadow: 0 1px 3px 0 rgb(0 0 0 / 0.1); max-width: 280px;
            font-family: 'Inter', sans-serif; line-height: 1.5;
        `;
        container.style.position = 'relative';
        container.appendChild(tooltip);
        instance._tooltipEl = tooltip;

        chart.subscribeCrosshairMove(param => {
            if (!param.time || !param.point || param.point.x < 0 || param.point.y < 0) {
                tooltip.style.display = 'none';
                return;
            }

            const dateStr = param.time;
            const ohlcvItem = data.ohlcv.find(d => d.date === dateStr);
            if (!ohlcvItem) {
                tooltip.style.display = 'none';
                return;
            }

            const change = ohlcvItem.close - ohlcvItem.open;
            const changePct = ((change / ohlcvItem.open) * 100).toFixed(2);
            const changeColor = change >= 0 ? '#16a34a' : '#dc2626';
            const changeSign = change >= 0 ? '+' : '';

            let html = `
                <div style="font-weight:600; margin-bottom:4px;">${dateStr}</div>
                <div style="display:grid; grid-template-columns: auto 1fr; gap: 2px 8px;">
                    <span style="color:#71717a">O</span><span>${ohlcvItem.open.toFixed(2)}</span>
                    <span style="color:#71717a">H</span><span>${ohlcvItem.high.toFixed(2)}</span>
                    <span style="color:#71717a">L</span><span>${ohlcvItem.low.toFixed(2)}</span>
                    <span style="color:#71717a">C</span><span style="font-weight:600; color:${changeColor}">${ohlcvItem.close.toFixed(2)}</span>
                    <span style="color:#71717a">Vol</span><span>${formatVolume(ohlcvItem.volume)}</span>
                </div>
                <div style="color:${changeColor}; font-weight:500; margin-top:4px;">
                    ${changeSign}${change.toFixed(2)} (${changeSign}${changePct}%)
                </div>
            `;

            // Add annotation if available
            const ann = annotations[dateStr];
            if (ann && ann.text) {
                html += `<div style="margin-top:6px; padding-top:6px; border-top:1px solid #e4e4e7; color:#71717a; font-size:10px;">${ann.text}</div>`;
            }

            tooltip.innerHTML = html;
            tooltip.style.display = 'block';

            // Position tooltip
            const chartRect = container.getBoundingClientRect();
            let left = param.point.x + 16;
            if (left + 280 > chartRect.width) left = param.point.x - 296;
            let top = param.point.y - 20;
            if (top < 0) top = 0;

            tooltip.style.left = left + 'px';
            tooltip.style.top = top + 'px';
        });
    },
};

function formatVolume(vol) {
    if (vol >= 1e9) return (vol / 1e9).toFixed(1) + 'B';
    if (vol >= 1e6) return (vol / 1e6).toFixed(1) + 'M';
    if (vol >= 1e3) return (vol / 1e3).toFixed(1) + 'K';
    return vol.toString();
}
