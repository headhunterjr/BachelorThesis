window.drawGrid = (canvasId, data) => {
    const { canvas, ctx } = window.clearCanvas(canvasId);
    if (!data || !data.demandProfile) return;

    const width = canvas.width;
    const height = canvas.height;

    const isMobile = width < 500;
    const leftPadding = isMobile ? 40 : 50;
    const rightPadding = isMobile ? 15 : 25;
    const topPadding = isMobile ? 25 : 40;
    const bottomPadding = isMobile ? 25 : 40;
    const fontSize = isMobile ? 9 : 12;
    const legendFontSize = isMobile ? 10 : 12;
    const yAxisFontSize = isMobile ? 8 : 10;

    const graphW = width - leftPadding - rightPadding;
    const graphH = height - topPadding - bottomPadding;
    const startX = leftPadding;
    const startY = height - bottomPadding;

    const steps = Math.max(1, data.demandProfile.length);

    let maxPower = 10;

    if (data.demandProfile && data.demandProfile.length > 0) {
        const demandMax = Math.max(...data.demandProfile);
        if (demandMax > maxPower) maxPower = demandMax;
    }
    if (data.solarProfile && data.solarProfile.length > 0) {
        const solarMax = Math.max(...data.solarProfile);
        if (solarMax > maxPower) maxPower = solarMax;
    }
    if (data.plannedBattery && data.plannedBattery.length > 0) {
        const batteryFiltered = data.plannedBattery.filter(v => v > 0);
        if (batteryFiltered.length > 0) {
            const batteryMax = Math.max(...batteryFiltered);
            if (batteryMax > maxPower) maxPower = batteryMax;
        }
    }
    if (data.plannedGrid && data.plannedGrid.length > 0) {
        const gridFiltered = data.plannedGrid.filter(v => v > 0);
        if (gridFiltered.length > 0) {
            const gridMax = Math.max(...gridFiltered);
            if (gridMax > maxPower) maxPower = gridMax;
        }
    }
    if (data.plannedGen && data.plannedGen.length > 0) {
        const genFiltered = data.plannedGen.filter(v => v > 0);
        if (genFiltered.length > 0) {
            const genMax = Math.max(...genFiltered);
            if (genMax > maxPower) maxPower = genMax;
        }
    }

    maxPower = Math.ceil(maxPower * 1.2);

    const toX = (i) => startX + (i / (steps - 1)) * graphW;
    const toY = (val) => startY - (val / maxPower) * graphH;

    ctx.strokeStyle = '#334155';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(startX, startY);
    ctx.lineTo(startX, topPadding);
    ctx.moveTo(startX, startY);
    ctx.lineTo(startX + graphW, startY);
    ctx.stroke();

    ctx.fillStyle = '#94A3B8';
    ctx.font = `${yAxisFontSize}px Inter, sans-serif`;
    ctx.textAlign = 'right';
    const yLabels = [0, maxPower / 2, maxPower];
    yLabels.forEach(val => {
        const y = toY(val);
        ctx.fillText(val.toFixed(1) + ' kW', startX - 5, y + 3);
    });
    ctx.textAlign = 'left';

    if (data.solarProfile) {
        ctx.fillStyle = 'rgba(251, 191, 36, 0.12)';
        ctx.beginPath();
        ctx.moveTo(startX, startY);
        data.solarProfile.forEach((val, i) => ctx.lineTo(toX(i), toY(val)));
        ctx.lineTo(toX(steps - 1), startY);
        ctx.closePath();
        ctx.fill();
    }

    if (data.plannedGen) {
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

    if (data.plannedGrid) {
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

    if (data.plannedBattery) {
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

    if (data.currentStep >= 0 && data.currentStep < steps - 1) {
        const xNow = toX(data.currentStep);
        ctx.strokeStyle = '#F1F5F9';
        ctx.lineWidth = 1;
        ctx.setLineDash([5, 5]);
        ctx.beginPath();
        ctx.moveTo(xNow, topPadding);
        ctx.lineTo(xNow, startY);
        ctx.stroke();
        ctx.setLineDash([]);

        ctx.fillStyle = '#F1F5F9';
        ctx.font = `${fontSize}px Inter, sans-serif`;

        const totalHours = (data.currentStep / (steps - 1)) * 24;
        const h = Math.floor(totalHours);
        const m = Math.round((totalHours - h) * 60);
        const timeLabel = `${h}:${m.toString().padStart(2, '0')}`;

        ctx.fillText(timeLabel, xNow + 5, topPadding + 10);
    }

    const barHeight = isMobile ? 8 : 10;
    const barY = startY + (isMobile ? 10 : 15);
    if (data.priceProfile) {
        for (let i = 0; i < data.priceProfile.length - 1; i++) {
            const price = data.priceProfile[i] || 0;
            const x = toX(i);
            const w = (graphW / (steps - 1)) + 1;

            let color = '#334155';
            if (price > 0.15) color = '#F59E0B';
            if (price > 0.25) color = '#EF4444';
            if (price > 5.0) color = '#000000';

            ctx.fillStyle = color;
            ctx.fillRect(x, barY, w, barHeight);
        }
    }

    ctx.font = `${legendFontSize}px Inter, sans-serif`;
    const legendY = topPadding - (isMobile ? 12 : 15);

    const legendItems = [
        { text: "● Акумулятор", color: '#10B981' },
        { text: "● Мережа", color: '#3B82F6' },
        { text: "● Генератор", color: '#F97316' },
        { text: "● Потреба", color: '#C084FC' }
    ];

    if (isMobile) {
        let lx = startX + 5;
        const spacing = 8;
        legendItems.forEach((item, idx) => {
            ctx.fillStyle = item.color;
            const textWidth = ctx.measureText(item.text).width;

            if (lx + textWidth > width - rightPadding && idx > 0) {
                lx = startX + 5;
            }

            ctx.fillText(item.text, lx, legendY);
            lx += textWidth + spacing;
        });
    } else {
        let lx = startX + 20;
        const spacing = 20;
        legendItems.forEach(item => {
            ctx.fillStyle = item.color;
            ctx.fillText(item.text, lx, legendY);
            lx += ctx.measureText(item.text).width + spacing;
        });
    }
};