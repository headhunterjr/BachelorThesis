window.drawParking = (canvasId, car, obstacles) => {
    const { canvas, ctx } = window.clearCanvas(canvasId);

    const scale = window.gameCanvas.scale;
    const centerX = canvas.width / 2;
    const centerY = canvas.height / 2;
    const toScreenX = (x) => centerX + (x * scale);
    const toScreenY = (y) => centerY - (y * scale);

    // === 1. DRAW TARGET (The Green Dot at 0,0) ===
    ctx.fillStyle = "#00FF00"; // Bright Green
    ctx.beginPath();
    // Draw a circle at (0,0) with radius 1 meter (scaled)
    ctx.arc(toScreenX(0), toScreenY(0), 1.0 * scale, 0, 2 * Math.PI);
    ctx.fill();

    // === 2. DRAW OBSTACLES ===
    if (obstacles) {
        ctx.fillStyle = "rgba(255, 0, 0, 0.5)";
        obstacles.forEach(obs => {
            ctx.beginPath();
            ctx.arc(toScreenX(obs.x), toScreenY(obs.y), obs.radius * scale, 0, 2 * Math.PI);
            ctx.fill();

            // Draw border
            ctx.strokeStyle = "darkred";
            ctx.lineWidth = 1;
            ctx.stroke();
        });
    }

    // === 3. DRAW CAR ===
    if (car) window.drawCar(ctx, car, toScreenX, toScreenY);
};