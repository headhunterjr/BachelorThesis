window.drawParking = (canvasId, car, obstacles) => {
    const { canvas, ctx } = window.clearCanvas(canvasId);
    if (!canvas || !ctx) return;

    const scale = window.gameCanvas.scale || 4.5;
    const origin = (window.gameCanvas.origins && window.gameCanvas.origins[canvasId])
        ? window.gameCanvas.origins[canvasId]
        : { x: canvas.width / 2, y: canvas.height / 2 };

    const toScreenX = (x) => origin.x + (x * scale);
    const toScreenY = (y) => origin.y - (y * scale);

    ctx.strokeStyle = '#334155';
    ctx.lineWidth = 1;
    const gridSize = 20 * scale;

    for (let x = origin.x % gridSize; x < canvas.width; x += gridSize) {
        ctx.beginPath();
        ctx.moveTo(x, 0);
        ctx.lineTo(x, canvas.height);
        ctx.stroke();
    }
    for (let y = origin.y % gridSize; y < canvas.height; y += gridSize) {
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(canvas.width, y);
        ctx.stroke();
    }

    ctx.strokeStyle = '#9CA3AF';
    ctx.lineWidth = 1;
    ctx.setLineDash([5, 5]);

    ctx.beginPath();
    ctx.moveTo(origin.x, 0);
    ctx.lineTo(origin.x, canvas.height);
    ctx.stroke();

    ctx.beginPath();
    ctx.moveTo(0, origin.y);
    ctx.lineTo(canvas.width, origin.y);
    ctx.stroke();

    ctx.setLineDash([]);

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

    ctx.strokeStyle = '#FFFFFF';
    ctx.lineWidth = 2;
    const markSize = targetRadius * 0.6;

    ctx.beginPath();
    ctx.moveTo(targetX - markSize, targetY);
    ctx.lineTo(targetX + markSize, targetY);
    ctx.moveTo(targetX, targetY - markSize);
    ctx.lineTo(targetX, targetY + markSize);
    ctx.stroke();

    if (obstacles) {
        obstacles.forEach(obs => {
            const obsX = toScreenX(obs.x);
            const obsY = toScreenY(obs.y);
            const obsRadius = obs.radius * scale;

            ctx.fillStyle = '#FEE2E2';
            ctx.beginPath();
            ctx.arc(obsX, obsY, obsRadius, 0, 2 * Math.PI);
            ctx.fill();

            ctx.strokeStyle = '#EF4444';
            ctx.lineWidth = 2;
            ctx.stroke();

            ctx.save();
            ctx.beginPath();
            ctx.arc(obsX, obsY, obsRadius, 0, Math.PI * 2);
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

    if (car) {
        const carX = toScreenX(car.x);
        const carY = toScreenY(car.y);
        const carLen = 4.0 * scale;
        const carWid = 2.0 * scale;

        ctx.save();
        ctx.translate(carX, carY);
        ctx.rotate(-car.theta);

        ctx.fillStyle = '#7C3AED';
        ctx.fillRect(-carLen / 2, -carWid / 2, carLen, carWid);

        ctx.strokeStyle = '#6D28D9';
        ctx.lineWidth = 2;
        ctx.strokeRect(-carLen / 2, -carWid / 2, carLen, carWid);

        ctx.fillStyle = 'rgba(255,255,255,0.3)';
        ctx.fillRect(-carLen / 2 + 4, -carWid / 2 + 3, carLen * 0.3, carWid - 6);

        ctx.fillStyle = '#14B8A6';
        ctx.beginPath();
        ctx.moveTo(carLen / 2 - 6, 0);
        ctx.lineTo(carLen / 2 + 4, -4);
        ctx.lineTo(carLen / 2 + 4, 4);
        ctx.closePath();
        ctx.fill();

        ctx.restore();

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

            const headlen = 8;
            const angle = Math.atan2(velY, velX);

            ctx.fillStyle = '#14B8A6';
            ctx.beginPath();
            ctx.moveTo(carX + velX, carY + velY);
            ctx.lineTo(carX + velX - headlen * Math.cos(angle - Math.PI / 6), carY + velY - headlen * Math.sin(angle - Math.PI / 6));
            ctx.lineTo(carX + velX - headlen * Math.cos(angle + Math.PI / 6), carY + velY - headlen * Math.sin(angle + Math.PI / 6));
            ctx.closePath();
            ctx.fill();
        }
    }
};
