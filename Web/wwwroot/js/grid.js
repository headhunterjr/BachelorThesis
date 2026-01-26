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

    // SCALES
    const steps = data.demandProfile.length;
    const maxPower = 120;

    const toX = (i) => startX + (i / (steps - 1)) * graphW;
    const toY = (val) => startY - (val / maxPower) * graphH;

    // 1. DRAW AXES
    ctx.strokeStyle = '#334155';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(startX, startY);
    ctx.lineTo(startX, padding);
    ctx.moveTo(startX, startY);
    ctx.lineTo(startX + graphW, startY);
    ctx.stroke();

    // 2. SOLAR (Filled)
    if (data.solarProfile) {
        ctx.fillStyle = 'rgba(251, 191, 36, 0.2)';
        ctx.beginPath();
        ctx.moveTo(startX, startY);
        data.solarProfile.forEach((val, i) => ctx.lineTo(toX(i), toY(val)));
        ctx.lineTo(toX(steps - 1), startY);
        ctx.fill();
    }

    // 3. DEMAND (Line)
    if (data.demandProfile) {
        ctx.strokeStyle = '#C084FC';
        ctx.lineWidth = 2;
        ctx.beginPath();
        data.demandProfile.forEach((val, i) => {
            if (i === 0) ctx.moveTo(toX(i), toY(val));
            else ctx.lineTo(toX(i), toY(val));
        });
        ctx.stroke();
    }

    // 4. BATTERY LEVEL (Green Line)
    if (data.plannedBattery && data.currentStep > 0) {
        ctx.strokeStyle = '#10B981';
        ctx.lineWidth = 3;
        ctx.shadowColor = '#10B981';
        ctx.shadowBlur = 10;

        ctx.beginPath();
        // Draw up to current step index
        for (let i = 0; i < data.currentStep && i < data.plannedBattery.length; i++) {
            const val = data.plannedBattery[i];
            if (i === 0) ctx.moveTo(toX(i), toY(val));
            else ctx.lineTo(toX(i), toY(val));
        }
        ctx.stroke();
        ctx.shadowBlur = 0;
    }

    // 5. CURRENT TIME MARKER
    if (data.currentStep >= 0 && data.currentStep < steps) {
        const xNow = toX(data.currentStep);

        ctx.strokeStyle = '#F1F5F9';
        ctx.lineWidth = 1;
        ctx.setLineDash([5, 5]);

        ctx.beginPath();
        ctx.moveTo(xNow, padding);
        ctx.lineTo(xNow, startY);
        ctx.stroke();
        ctx.setLineDash([]);

        // Time Label
        ctx.fillStyle = '#F1F5F9';
        ctx.font = '10px Inter, sans-serif';
        const hour = Math.floor((data.currentStep / steps) * 24);
        ctx.fillText(`${hour}:00`, xNow + 5, padding + 10);
    }

    // 6. PRICE (Bottom Bar)
    const barHeight = 10;
    const barY = startY + 15;
    data.priceProfile.forEach((price, i) => {
        const x = toX(i);
        const w = (graphW / (steps - 1)) + 1;
        let color = '#334155'; // Base
        if (price > 0.4) color = '#EF4444'; // Peak
        if (price > 100) color = '#000000'; // Blackout
        ctx.fillStyle = color;
        ctx.fillRect(x, barY, w, barHeight);
    });

    // LEGEND
    ctx.font = '12px Inter, sans-serif';
    ctx.fillStyle = '#94A3B8';

    ctx.fillStyle = '#FBBF24'; ctx.fillText("● Solar", startX + 20, padding);
    ctx.fillStyle = '#C084FC'; ctx.fillText("● Demand", startX + 80, padding);
    ctx.fillStyle = '#10B981'; ctx.fillText("● Battery", startX + 160, padding);
    ctx.fillStyle = '#EF4444'; ctx.fillText("■ High Price", startX + 240, padding);
};