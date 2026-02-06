window.drawGrid = (canvasId, data) => {
    const { canvas, ctx } = window.clearCanvas(canvasId);
    if (!data || !data.demandProfile) return;

    const width = canvas.width;
    const height = canvas.height;
    const padding = 40;
    const graphW = width - (padding * 2);
    const graphH = height - (padding * 2);
    const startX = padding;
    const startY = height - padding;

    const steps = Math.max(1, data.demandProfile.length);
    const maxPower = 120;

    const toX = (i) => startX + (i / (steps - 1)) * graphW;
    const toY = (val) => startY - (val / maxPower) * graphH;

    // Axes
    ctx.strokeStyle = '#334155';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(startX, startY);
    ctx.lineTo(startX, padding);
    ctx.moveTo(startX, startY);
    ctx.lineTo(startX + graphW, startY);
    ctx.stroke();

    // SOLAR (filled)
    if (data.solarProfile) {
        ctx.fillStyle = 'rgba(251, 191, 36, 0.12)';
        ctx.beginPath();
        ctx.moveTo(startX, startY);
        data.solarProfile.forEach((val, i) => ctx.lineTo(toX(i), toY(val)));
        ctx.lineTo(toX(steps - 1), startY);
        ctx.closePath();
        ctx.fill();
    }

    // PLANNED GENERATOR (dashed orange)
    if (data.plannedGen) {
        // Fix: Use currentStep + 1 to include the current point in the line segment
        const limit = data.currentStep >= (steps - 1) ? data.plannedGen.length : data.currentStep + 1;
        const visible = Math.min(limit, data.plannedGen.length);

        ctx.strokeStyle = '#F97316';
        ctx.lineWidth = 2;
        ctx.setLineDash([3, 3]);
        ctx.beginPath();
        for (let i = 0; i < visible; i++) {
            const val = data.plannedGen[i] || 0;
            const yVal = Math.max(0, val);
            const x = toX(i);
            const y = toY(yVal);
            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        }
        ctx.stroke();
        ctx.setLineDash([]);
    }

    // PLANNED GRID (blue)
    if (data.plannedGrid) {
        // Fix: Use currentStep + 1
        const limit = data.currentStep >= (steps - 1) ? data.plannedGrid.length : data.currentStep + 1;
        const visible = Math.min(limit, data.plannedGrid.length);

        ctx.strokeStyle = '#3B82F6';
        ctx.lineWidth = 2;
        ctx.beginPath();
        for (let i = 0; i < visible; i++) {
            const val = data.plannedGrid[i] || 0;
            const yVal = Math.max(0, val);
            const x = toX(i);
            const y = toY(yVal);
            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        }
        ctx.stroke();
    }

    // DEMAND (full across entire profile)
    if (data.demandProfile) {
        ctx.strokeStyle = 'rgba(192, 132, 252, 0.4)';
        ctx.lineWidth = 4;
        ctx.beginPath();
        data.demandProfile.forEach((val, i) => {
            const x = toX(i);
            const y = toY(val || 0);
            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        });
        ctx.stroke();
    }

    // BATTERY (green)
    if (data.plannedBattery) {
        // Fix: Use currentStep + 1
        const limit = data.currentStep >= (steps - 1) ? data.plannedBattery.length : data.currentStep + 1;
        const visible = Math.min(limit, data.plannedBattery.length);

        ctx.strokeStyle = '#10B981';
        ctx.lineWidth = 3;
        ctx.shadowColor = '#10B981';
        ctx.shadowBlur = 10;
        ctx.beginPath();
        for (let i = 0; i < visible; i++) {
            const val = data.plannedBattery[i] || 0;
            const x = toX(i);
            const y = toY(val);
            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        }
        ctx.stroke();
        ctx.shadowBlur = 0;
    }

    // TIME INDICATOR (dashed vertical)
    if (data.currentStep >= 0 && data.currentStep < steps - 1) {
        const xNow = toX(data.currentStep);
        ctx.strokeStyle = '#F1F5F9';
        ctx.lineWidth = 1;
        ctx.setLineDash([5, 5]);
        ctx.beginPath();
        ctx.moveTo(xNow, padding);
        ctx.lineTo(xNow, startY);
        ctx.stroke();
        ctx.setLineDash([]);

        ctx.fillStyle = '#F1F5F9';
        ctx.font = '10px Inter, sans-serif';

        // Fix: Calculate specific Hour and Minute for HH:MM format
        const totalHours = (data.currentStep / (steps - 1)) * 24;
        const h = Math.floor(totalHours);
        const m = Math.round((totalHours - h) * 60);
        // Format as 13:00 or 13:15
        const timeLabel = `${h}:${m.toString().padStart(2, '0')}`;

        ctx.fillText(timeLabel, xNow + 5, padding + 10);
    }

    // PRICE BARS across whole profile
    const barHeight = 10;
    const barY = startY + 15;
    if (data.priceProfile) {
        // Fix: Stop 1 step early. Profile points = N, Intervals = N-1.
        // If we draw N bars, the last one overflows the chart.
        for (let i = 0; i < data.priceProfile.length - 1; i++) {
            const price = data.priceProfile[i] || 0;
            const x = toX(i);
            const w = (graphW / (steps - 1)) + 1; // +1 to overlap gaps slightly
            let color = '#334155';
            if (price > 0.4) color = '#EF4444';
            if (price > 100) color = '#000000';
            ctx.fillStyle = color;
            ctx.fillRect(x, barY, w, barHeight);
        }
    }

    // LEGEND
    ctx.font = '12px Inter, sans-serif';
    let lx = startX + 20;
    const spacing = 20;
    const legendItems = [
        { text: "● Акумулятор", color: '#10B981' },
        { text: "● Мережа", color: '#3B82F6' },
        { text: "● Генератор", color: '#F97316' },
        { text: "● Потреба", color: '#C084FC' }
    ];
    legendItems.forEach(item => {
        ctx.fillStyle = item.color;
        ctx.fillText(item.text, lx, padding);
        lx += ctx.measureText(item.text).width + spacing;
    });
};