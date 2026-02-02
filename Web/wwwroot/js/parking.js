window.drawParking = (canvasId, car, obstacles) => {
    const { canvas, ctx } = window.clearCanvas(canvasId);
    const scale = window.gameCanvas.scale;
    const centerX = canvas.width / 2;
    const centerY = canvas.height / 2;

    const toScreenX = (x) => centerX + (x * scale);
    const toScreenY = (y) => centerY - (y * scale);

    // Background grid
    ctx.strokeStyle = '#334155';
    ctx.lineWidth = 1;
    const gridSize = 20 * scale;

    for (let x = centerX % gridSize; x < canvas.width; x += gridSize) {
        ctx.beginPath();
        ctx.moveTo(x, 0);
        ctx.lineTo(x, canvas.height);
        ctx.stroke();
    }

    for (let y = centerY % gridSize; y < canvas.height; y += gridSize) {
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(canvas.width, y);
        ctx.stroke();
    }

    // Center axes
    ctx.strokeStyle = '#9CA3AF';
    ctx.lineWidth = 1;
    ctx.setLineDash([5, 5]);

    ctx.beginPath();
    ctx.moveTo(centerX, 0);
    ctx.lineTo(centerX, canvas.height);
    ctx.stroke();

    ctx.beginPath();
    ctx.moveTo(0, centerY);
    ctx.lineTo(canvas.width, centerY);
    ctx.stroke();

    ctx.setLineDash([]);

    // Target at (0,0)
    const targetX = toScreenX(0);
    const targetY = toScreenY(0);
    const targetRadius = 1.5 * scale;

    ctx.fillStyle = '#10B981';
    ctx.beginPath();
    ctx.arc(targetX, targetY, targetRadius, 0, 2 * Math.PI);
    ctx.fill();

    ctx.strokeStyle = '#059669';
    ctx.lineWidth = 2;
    ctx.stroke();

    // Target center mark
    ctx.strokeStyle = '#FFFFFF';
    ctx.lineWidth = 2;
    const markSize = targetRadius * 0.6;

    ctx.beginPath();
    ctx.moveTo(targetX - markSize, targetY);
    ctx.lineTo(targetX + markSize, targetY);
    ctx.moveTo(targetX, targetY - markSize);
    ctx.lineTo(targetX, targetY + markSize);
    ctx.stroke();

    // Obstacles
    if (obstacles) {
        obstacles.forEach(obs => {
            const obsX = toScreenX(obs.x);
            const obsY = toScreenY(obs.y);
            const obsRadius = obs.radius * scale;

            // Obstacle circle
            ctx.fillStyle = '#FEE2E2';
            ctx.beginPath();
            ctx.arc(obsX, obsY, obsRadius, 0, 2 * Math.PI);
            ctx.fill();

            ctx.strokeStyle = '#EF4444';
            ctx.lineWidth = 2;
            ctx.stroke();

            // Diagonal stripes
            ctx.save();
            ctx.clip();
            ctx.strokeStyle = '#FECACA';
            ctx.lineWidth = 1;

            for (let i = -obsRadius; i < obsRadius; i += 6) {
                ctx.beginPath();
                ctx.moveTo(obsX + i - obsRadius, obsY - obsRadius);
                ctx.lineTo(obsX + i + obsRadius, obsY + obsRadius);
                ctx.stroke();
            }

            ctx.restore();
        });
    }

    // Car
    if (car) {
        const carX = toScreenX(car.x);
        const carY = toScreenY(car.y);
        const carLen = 4.0 * scale;
        const carWid = 2.0 * scale;

        ctx.save();
        ctx.translate(carX, carY);
        ctx.rotate(-car.theta);

        // Car body
        ctx.fillStyle = '#7C3AED';
        ctx.fillRect(-carLen / 2, -carWid / 2, carLen, carWid);

        ctx.strokeStyle = '#6D28D9';
        ctx.lineWidth = 2;
        ctx.strokeRect(-carLen / 2, -carWid / 2, carLen, carWid);

        // Windows
        ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
        ctx.fillRect(-carLen / 2 + 4, -carWid / 2 + 3, carLen * 0.3, carWid - 6);

        // Direction indicator
        ctx.fillStyle = '#14B8A6';
        ctx.beginPath();
        ctx.moveTo(carLen / 2 - 6, 0);
        ctx.lineTo(carLen / 2 + 4, -4);
        ctx.lineTo(carLen / 2 + 4, 4);
        ctx.closePath();
        ctx.fill();

        ctx.restore();

        // Velocity vector
        if (car.velocity && Math.abs(car.velocity) > 0.1) {
            const velScale = 10;
            const velX = Math.cos(car.theta) * car.velocity * velScale;
            const velY = -Math.sin(car.theta) * car.velocity * velScale;

            ctx.strokeStyle = '#14B8A6';
            ctx.lineWidth = 2;
            ctx.lineCap = 'round';

            ctx.beginPath();
            ctx.moveTo(carX, carY);
            ctx.lineTo(carX + velX, carY + velY);
            ctx.stroke();

            // Arrow head
            const headlen = 8;
            const angle = Math.atan2(velY, velX);

            ctx.fillStyle = '#14B8A6';
            ctx.beginPath();
            ctx.moveTo(carX + velX, carY + velY);
            ctx.lineTo(
                carX + velX - headlen * Math.cos(angle - Math.PI / 6),
                carY + velY - headlen * Math.sin(angle - Math.PI / 6)
            );
            ctx.lineTo(
                carX + velX - headlen * Math.cos(angle + Math.PI / 6),
                carY + velY - headlen * Math.sin(angle + Math.PI / 6)
            );
            ctx.closePath();
            ctx.fill();
        }
    }
};