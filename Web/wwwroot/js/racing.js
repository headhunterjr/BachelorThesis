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
    const rawPoints = data.rawPoints || data.RawPoints;

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

    ctx.strokeStyle = '#475569';
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

    if (rawPoints && rawPoints.length > 0) {
        ctx.strokeStyle = '#7C3AED';
        ctx.lineWidth = 3;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';

        ctx.beginPath();
        rawPoints.forEach((p, index) => {
            const sx = toScreenX(p[0]);
            const sy = toScreenY(p[1]);
            if (index === 0) ctx.moveTo(sx, sy);
            else ctx.lineTo(sx, sy);
        });
        ctx.stroke();

        ctx.fillStyle = '#7C3AED';
        rawPoints.forEach((p) => {
            const sx = toScreenX(p[0]);
            const sy = toScreenY(p[1]);
            ctx.beginPath();
            ctx.arc(sx, sy, 4, 0, 2 * Math.PI);
            ctx.fill();
        });
    }

    if (track && track.length > 0) {
        ctx.strokeStyle = 'rgba(124, 58, 237, 0.1)';
        ctx.lineWidth = 15 * scale;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.beginPath();
        track.forEach((p, index) => {
            const sx = toScreenX(p[0]);
            const sy = toScreenY(p[1]);
            if (index === 0) ctx.moveTo(sx, sy);
            else ctx.lineTo(sx, sy);
        });
        ctx.stroke();

        ctx.strokeStyle = '#7C3AED';
        ctx.lineWidth = 2;
        ctx.beginPath();
        track.forEach((p, index) => {
            const sx = toScreenX(p[0]);
            const sy = toScreenY(p[1]);
            if (index === 0) ctx.moveTo(sx, sy);
            else ctx.lineTo(sx, sy);
        });
        ctx.stroke();
    }

    if (trail && trail.length > 0) {
        ctx.strokeStyle = '#14B8A6';
        ctx.lineWidth = 2;
        ctx.shadowBlur = 8;
        ctx.shadowColor = '#14B8A6';

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

    if (car && track && track.length > 0) {
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

        ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
        ctx.fillRect(-carLen / 2 + 4, -carWid / 2 + 3, carLen * 0.3, carWid - 6);

        ctx.fillStyle = '#14B8A6';
        ctx.beginPath();
        ctx.moveTo(carLen / 2 - 6, 0);
        ctx.lineTo(carLen / 2 + 4, -4);
        ctx.lineTo(carLen / 2 + 4, 4);
        ctx.closePath();
        ctx.fill();

        ctx.restore();
    }
};