window.drawRacing = (canvasId, car, trackPoints) => {
    const { canvas, ctx } = window.clearCanvas(canvasId);

    const scale = window.gameCanvas.scale;
    const centerX = canvas.width / 2;
    const centerY = canvas.height / 2;
    const toScreenX = (x) => centerX + (x * scale);
    const toScreenY = (y) => centerY - (y * scale);

    // Draw Track
    if (trackPoints && trackPoints.length > 0) {
        // 1. Draw Center Line
        ctx.strokeStyle = "#aaaaaa";
        ctx.lineWidth = 2;
        ctx.beginPath();
        trackPoints.forEach((p, index) => {
            const sx = toScreenX(p[0]);
            const sy = toScreenY(p[1]);
            if (index === 0) ctx.moveTo(sx, sy);
            else ctx.lineTo(sx, sy);
        });
        ctx.stroke();

        // 2. Draw Boundaries (Visual reference 10m wide)
        ctx.strokeStyle = "rgba(0, 0, 0, 0.05)";
        ctx.lineWidth = 10 * scale;
        ctx.lineCap = "round";
        ctx.lineJoin = "round";
        ctx.beginPath();
        trackPoints.forEach((p, index) => {
            const sx = toScreenX(p[0]);
            const sy = toScreenY(p[1]);
            if (index === 0) ctx.moveTo(sx, sy);
            else ctx.lineTo(sx, sy);
        });
        ctx.stroke();
    }

    // Draw Car
    if (car) window.drawCar(ctx, car, toScreenX, toScreenY);
};