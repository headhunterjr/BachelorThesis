window.drawParking = (canvasId, car, obstacles) => {
    const { canvas, ctx } = window.clearCanvas(canvasId);
    const scale = window.gameCanvas.scale;
    const centerX = canvas.width / 2;
    const centerY = canvas.height / 2;

    const toScreenX = (x) => centerX + (x * scale);
    const toScreenY = (y) => centerY - (y * scale);

    // === BACKGROUND GRID ===
    ctx.strokeStyle = 'rgba(139, 92, 246, 0.08)';
    ctx.lineWidth = 1;
    const gridSize = 10 * scale;

    // Vertical lines
    for (let x = centerX % gridSize; x < canvas.width; x += gridSize) {
        ctx.beginPath();
        ctx.moveTo(x, 0);
        ctx.lineTo(x, canvas.height);
        ctx.stroke();
    }

    // Horizontal lines
    for (let y = centerY % gridSize; y < canvas.height; y += gridSize) {
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(canvas.width, y);
        ctx.stroke();
    }

    // === CENTER CROSSHAIR ===
    ctx.strokeStyle = 'rgba(139, 92, 246, 0.3)';
    ctx.lineWidth = 1;
    ctx.setLineDash([5, 5]);

    // Vertical center line
    ctx.beginPath();
    ctx.moveTo(centerX, 0);
    ctx.lineTo(centerX, canvas.height);
    ctx.stroke();

    // Horizontal center line
    ctx.beginPath();
    ctx.moveTo(0, centerY);
    ctx.lineTo(canvas.width, centerY);
    ctx.stroke();

    ctx.setLineDash([]);

    // === TARGET (The Goal at 0,0) ===
    const targetX = toScreenX(0);
    const targetY = toScreenY(0);
    const targetRadius = 1.0 * scale;

    // Outer glow rings
    for (let i = 3; i > 0; i--) {
        ctx.beginPath();
        ctx.arc(targetX, targetY, targetRadius + (i * 8), 0, 2 * Math.PI);
        const alpha = 0.1 - (i * 0.02);
        ctx.fillStyle = `rgba(16, 185, 129, ${alpha})`;
        ctx.fill();
    }

    // Main target circle with gradient
    const targetGradient = ctx.createRadialGradient(targetX, targetY, 0, targetX, targetY, targetRadius);
    targetGradient.addColorStop(0, '#34D399');
    targetGradient.addColorStop(0.7, '#10B981');
    targetGradient.addColorStop(1, '#059669');

    ctx.fillStyle = targetGradient;
    ctx.beginPath();
    ctx.arc(targetX, targetY, targetRadius, 0, 2 * Math.PI);
    ctx.fill();

    // Target border
    ctx.strokeStyle = '#10B981';
    ctx.lineWidth = 2;
    ctx.stroke();

    // Inner highlight
    ctx.fillStyle = 'rgba(255, 255, 255, 0.4)';
    ctx.beginPath();
    ctx.arc(targetX - targetRadius * 0.3, targetY - targetRadius * 0.3, targetRadius * 0.3, 0, 2 * Math.PI);
    ctx.fill();

    // Target crosshair
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.8)';
    ctx.lineWidth = 2;
    const crossSize = targetRadius * 0.5;

    ctx.beginPath();
    ctx.moveTo(targetX - crossSize, targetY);
    ctx.lineTo(targetX + crossSize, targetY);
    ctx.moveTo(targetX, targetY - crossSize);
    ctx.lineTo(targetX, targetY + crossSize);
    ctx.stroke();

    // === OBSTACLES ===
    if (obstacles) {
        obstacles.forEach((obs, index) => {
            const obsX = toScreenX(obs.x);
            const obsY = toScreenY(obs.y);
            const obsRadius = obs.radius * scale;

            // Outer glow
            const glowGradient = ctx.createRadialGradient(obsX, obsY, obsRadius * 0.5, obsX, obsY, obsRadius * 1.5);
            glowGradient.addColorStop(0, 'rgba(239, 68, 68, 0.3)');
            glowGradient.addColorStop(1, 'rgba(239, 68, 68, 0)');

            ctx.fillStyle = glowGradient;
            ctx.beginPath();
            ctx.arc(obsX, obsY, obsRadius * 1.5, 0, 2 * Math.PI);
            ctx.fill();

            // Main obstacle with gradient
            const obstacleGradient = ctx.createRadialGradient(
                obsX - obsRadius * 0.3,
                obsY - obsRadius * 0.3,
                0,
                obsX,
                obsY,
                obsRadius
            );
            obstacleGradient.addColorStop(0, '#F87171');
            obstacleGradient.addColorStop(0.6, '#EF4444');
            obstacleGradient.addColorStop(1, '#DC2626');

            ctx.fillStyle = obstacleGradient;
            ctx.beginPath();
            ctx.arc(obsX, obsY, obsRadius, 0, 2 * Math.PI);
            ctx.fill();

            // Border
            ctx.strokeStyle = '#991B1B';
            ctx.lineWidth = 2;
            ctx.stroke();

            // Inner shadow
            ctx.fillStyle = 'rgba(0, 0, 0, 0.2)';
            ctx.beginPath();
            ctx.arc(obsX + obsRadius * 0.2, obsY + obsRadius * 0.2, obsRadius * 0.8, 0, 2 * Math.PI);
            ctx.fill();

            // Highlight
            ctx.fillStyle = 'rgba(255, 255, 255, 0.2)';
            ctx.beginPath();
            ctx.arc(obsX - obsRadius * 0.4, obsY - obsRadius * 0.4, obsRadius * 0.3, 0, 2 * Math.PI);
            ctx.fill();

            // Warning stripes
            ctx.save();
            ctx.translate(obsX, obsY);
            ctx.strokeStyle = 'rgba(153, 27, 27, 0.5)';
            ctx.lineWidth = 2;

            for (let i = 0; i < 8; i++) {
                const angle = (i / 8) * Math.PI * 2;
                ctx.beginPath();
                ctx.moveTo(Math.cos(angle) * obsRadius * 0.6, Math.sin(angle) * obsRadius * 0.6);
                ctx.lineTo(Math.cos(angle) * obsRadius * 0.9, Math.sin(angle) * obsRadius * 0.9);
                ctx.stroke();
            }

            ctx.restore();
        });
    }

    // === CAR ===
    if (car) {
        const carX = toScreenX(car.x);
        const carY = toScreenY(car.y);
        const carLen = 4.0 * scale;
        const carWid = 2.0 * scale;

        ctx.save();
        ctx.translate(carX, carY);
        ctx.rotate(-car.theta);

        // Car shadow
        ctx.fillStyle = 'rgba(0, 0, 0, 0.3)';
        ctx.fillRect(-carLen / 2 + 3, -carWid / 2 + 3, carLen, carWid);

        // Car body gradient
        const carGradient = ctx.createLinearGradient(0, -carWid / 2, 0, carWid / 2);
        carGradient.addColorStop(0, '#8B5CF6');
        carGradient.addColorStop(0.5, '#6B46C1');
        carGradient.addColorStop(1, '#553399');

        ctx.fillStyle = carGradient;
        ctx.fillRect(-carLen / 2, -carWid / 2, carLen, carWid);

        // Car border
        ctx.strokeStyle = '#A78BFA';
        ctx.lineWidth = 2;
        ctx.strokeRect(-carLen / 2, -carWid / 2, carLen, carWid);

        // Windows
        ctx.fillStyle = 'rgba(139, 92, 246, 0.3)';
        ctx.fillRect(-carLen / 2 + 4, -carWid / 2 + 2, carLen * 0.3, carWid - 4);
        ctx.fillRect(carLen / 2 - 4 - carLen * 0.3, -carWid / 2 + 2, carLen * 0.3, carWid - 4);

        // Windshield highlight
        ctx.fillStyle = 'rgba(255, 255, 255, 0.2)';
        ctx.fillRect(-carLen / 2 + 4, -carWid / 2 + 2, carLen * 0.25, carWid * 0.3);

        // Front direction indicator (arrow)
        ctx.fillStyle = '#22D3EE';
        ctx.beginPath();
        ctx.moveTo(carLen / 2 - 8, 0);
        ctx.lineTo(carLen / 2 + 4, 0);
        ctx.lineTo(carLen / 2, -6);
        ctx.closePath();
        ctx.fill();

        ctx.beginPath();
        ctx.moveTo(carLen / 2 - 8, 0);
        ctx.lineTo(carLen / 2 + 4, 0);
        ctx.lineTo(carLen / 2, 6);
        ctx.closePath();
        ctx.fill();

        // Glow effect on direction indicator
        ctx.shadowColor = '#22D3EE';
        ctx.shadowBlur = 10;
        ctx.fillStyle = '#22D3EE';
        ctx.fillRect(carLen / 2 - 2, -1, 4, 2);
        ctx.shadowBlur = 0;

        // Wheels
        ctx.fillStyle = '#1F2937';
        const wheelWidth = 3;
        const wheelHeight = carWid * 0.3;

        // Front wheels
        ctx.fillRect(carLen / 2 - 8, -carWid / 2 - wheelHeight / 2, wheelWidth, wheelHeight);
        ctx.fillRect(carLen / 2 - 8, carWid / 2 - wheelHeight / 2, wheelWidth, wheelHeight);

        // Rear wheels
        ctx.fillRect(-carLen / 2 + 5, -carWid / 2 - wheelHeight / 2, wheelWidth, wheelHeight);
        ctx.fillRect(-carLen / 2 + 5, carWid / 2 - wheelHeight / 2, wheelWidth, wheelHeight);

        ctx.restore();

        // Velocity vector
        if (car.velocity && Math.abs(car.velocity) > 0.1) {
            const velScale = 15;
            const velX = Math.cos(car.theta) * car.velocity * velScale;
            const velY = -Math.sin(car.theta) * car.velocity * velScale;

            // Gradient for velocity vector
            const velGradient = ctx.createLinearGradient(carX, carY, carX + velX, carY + velY);
            velGradient.addColorStop(0, 'rgba(34, 211, 238, 0.8)');
            velGradient.addColorStop(1, 'rgba(34, 211, 238, 0.2)');

            ctx.strokeStyle = velGradient;
            ctx.lineWidth = 3;
            ctx.lineCap = 'round';

            // Draw arrow
            ctx.beginPath();
            ctx.moveTo(carX, carY);
            ctx.lineTo(carX + velX, carY + velY);
            ctx.stroke();

            // Arrow head
            const headlen = 10;
            const angle = Math.atan2(velY, velX);

            ctx.fillStyle = '#22D3EE';
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

        // Distance indicator to target
        const distToTarget = Math.sqrt(car.x * car.x + car.y * car.y);
        if (distToTarget > 2) {
            ctx.font = '12px "JetBrains Mono", monospace';
            ctx.fillStyle = '#A78BFA';
            ctx.textAlign = 'center';
            ctx.fillText(`${distToTarget.toFixed(1)}m`, carX, carY - carWid / 2 - 10);
        }
    }

    // === COORDINATES DISPLAY ===
    ctx.font = '11px "JetBrains Mono", monospace';
    ctx.fillStyle = 'rgba(168, 168, 192, 0.6)';
    ctx.textAlign = 'left';
    ctx.fillText('(0, 0)', centerX + 5, centerY - 5);
};