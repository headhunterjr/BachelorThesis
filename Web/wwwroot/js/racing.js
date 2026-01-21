window.drawRacing = (canvasId, car, data) => {
    const { canvas, ctx } = window.clearCanvas(canvasId);

    const scale = window.gameCanvas.scale;
    const centerX = canvas.width / 2;
    const centerY = canvas.height / 2;
    const toScreenX = (x) => centerX + (x * scale);
    const toScreenY = (y) => centerY - (y * scale);

    if (!data) return;

    const track = data.track || data.Track;
    const trail = data.trail || data.Trail;

    // 1. Draw Reference Track (Gray)
    if (track && track.length > 0) {
        ctx.strokeStyle = "#aaaaaa";
        ctx.lineWidth = 2;
        ctx.beginPath();
        track.forEach((p, index) => {
            const sx = toScreenX(p[0]);
            const sy = toScreenY(p[1]);
            if (index === 0) ctx.moveTo(sx, sy);
            else ctx.lineTo(sx, sy);
        });
        ctx.stroke();

        // Boundaries
        ctx.strokeStyle = "rgba(0, 0, 0, 0.05)";
        ctx.lineWidth = 15 * scale;
        ctx.lineCap = "round";
        ctx.lineJoin = "round";
        ctx.beginPath();
        track.forEach((p, index) => {
            const sx = toScreenX(p[0]);
            const sy = toScreenY(p[1]);
            if (index === 0) ctx.moveTo(sx, sy);
            else ctx.lineTo(sx, sy);
        });
        ctx.stroke();
    }

    // 2. Draw Car Trail (Cyan)
    if (trail && trail.length > 0) {
        ctx.strokeStyle = "cyan";
        ctx.lineWidth = 2;
        ctx.shadowBlur = 5;
        ctx.shadowColor = "cyan"; // Neon glow effect

        ctx.beginPath();
        trail.forEach((p, index) => {
            const sx = toScreenX(p[0]);
            const sy = toScreenY(p[1]);
            if (index === 0) ctx.moveTo(sx, sy);
            else ctx.lineTo(sx, sy);
        });
        ctx.stroke();

        ctx.shadowBlur = 0;
    }

    // 3. Draw Car
    if (car) window.drawCar(ctx, car, toScreenX, toScreenY);
};