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

    const steps = data.demandProfile.length;
    const maxPower = 120; // Fixed scale for stability

    const toX = (i) => startX + (i / (steps - 1)) * graphW;
    const toY = (val) => startY - (val / maxPower) * graphH;

    // 1. AXES
    ctx.strokeStyle = '#334155';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(startX, startY);
    ctx.lineTo(startX, padding);
    ctx.moveTo(startX, startY);
    ctx.lineTo(startX + graphW, startY);
    ctx.stroke();

    // 2. SOLAR (Fill)
    if (data.solarProfile) {
        ctx.fillStyle = 'rgba(251, 191, 36, 0.1)';
        ctx.beginPath();
        ctx.moveTo(startX, startY);
        data.solarProfile.forEach((val, i) => ctx.lineTo(toX(i), toY(val)));
        ctx.lineTo(toX(steps - 1), startY);
        ctx.fill();
    }

    // --- NEW LINES ---

    // 3. GENERATOR (Orange Line)
    if (data.plannedGen && data.currentStep > 0) {
        ctx.strokeStyle = '#F97316'; // Orange
        ctx.lineWidth = 2;
        ctx.setLineDash([2, 2]); // Dotted to differentiate from Grid
        ctx.beginPath();
        for (let i = 0; i < data.currentStep && i < data.plannedGen.length; i++) {
            const val = data.plannedGen[i];
            // Clip negatives visually (shouldn't happen with generator but safe to do)
            const yVal = Math.max(0, val);
            if (i === 0) ctx.moveTo(toX(i), toY(yVal));
            else ctx.lineTo(toX(i), toY(yVal));
        }
        ctx.stroke();
        ctx.setLineDash([]);
    }

    // 4. GRID IMPORT (Blue Line)
    if (data.plannedGrid && data.currentStep > 0) {
        ctx.strokeStyle = '#3B82F6'; // Blue
        ctx.lineWidth = 2;
        ctx.beginPath();
        for (let i = 0; i < data.currentStep && i < data.plannedGrid.length; i++) {
            const val = data.plannedGrid[i];
            // Grid can be negative (export). We map it directly.
            // If it goes below y-axis, it just clips off bottom or we could center axis.
            // For now, let's clamp visual at 0 for simplicity or allow it to dip? 
            // Let's allow dip visually but maybe clamp strictly for this chart type.
            const yVal = val;
            if (i === 0) ctx.moveTo(toX(i), toY(yVal));
            else ctx.lineTo(toX(i), toY(yVal));
        }
        ctx.stroke();
    }

    // 5. DEMAND (Purple Line - Background Context)
    if (data.demandProfile) {
        ctx.strokeStyle = 'rgba(192, 132, 252, 0.4)'; // Purple (Faded)
        ctx.lineWidth = 4;
        ctx.beginPath();
        data.demandProfile.forEach((val, i) => {
            if (i === 0) ctx.moveTo(toX(i), toY(val));
            else ctx.lineTo(toX(i), toY(val));
        });
        ctx.stroke();
    }

    // 6. BATTERY LEVEL (Green Line - Main Focus)
    if (data.plannedBattery && data.currentStep > 0) {
        ctx.strokeStyle = '#10B981';
        ctx.lineWidth = 3;
        ctx.shadowColor = '#10B981';
        ctx.shadowBlur = 10;
        ctx.beginPath();
        for (let i = 0; i < data.currentStep && i < data.plannedBattery.length; i++) {
            const val = data.plannedBattery[i];
            if (i === 0) ctx.moveTo(toX(i), toY(val));
            else ctx.lineTo(toX(i), toY(val));
        }
        ctx.stroke();
        ctx.shadowBlur = 0;
    }

    // 7. TIME MARKER
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

        ctx.fillStyle = '#F1F5F9';
        ctx.font = '10px Inter, sans-serif';
        const hour = Math.floor((data.currentStep / steps) * 24);
        ctx.fillText(`${hour}:00`, xNow + 5, padding + 10);
    }

    // 8. PRICE BAR
    const barHeight = 10;
    const barY = startY + 15;
    data.priceProfile.forEach((price, i) => {
        const x = toX(i);
        const w = (graphW / (steps - 1)) + 1;
        let color = '#334155';
        if (price > 0.4) color = '#EF4444';
        if (price > 100) color = '#000000';
        ctx.fillStyle = color;
        ctx.fillRect(x, barY, w, barHeight);
    });

    // LEGEND
    ctx.font = '12px Inter, sans-serif';
    ctx.fillStyle = '#94A3B8';

    let lx = startX + 20;
    ctx.fillStyle = '#10B981'; ctx.fillText("● Battery", lx, padding); lx += 70;
    ctx.fillStyle = '#3B82F6'; ctx.fillText("● Grid", lx, padding); lx += 60;
    ctx.fillStyle = '#F97316'; ctx.fillText("● Gen", lx, padding); lx += 60;
    ctx.fillStyle = '#C084FC'; ctx.fillText("● Demand", lx, padding);
};