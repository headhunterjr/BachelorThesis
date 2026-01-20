window.gameCanvas = {
    scale: 5.0
};

window.initCanvasEvents = (canvasId, dotNetHelper) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const rect = () => canvas.getBoundingClientRect();
    const scale = window.gameCanvas.scale;

    const toMeters = (clientX, clientY) => {
        const r = rect();
        const centerX = canvas.width / 2;
        const centerY = canvas.height / 2;
        const rawX = clientX - r.left;
        const rawY = clientY - r.top;
        const x = (rawX - centerX) / scale;
        const y = -(rawY - centerY) / scale;
        return { x, y };
    };

    canvas.onmousedown = (e) => {
        const c = toMeters(e.clientX, e.clientY);
        if (e.button === 2) dotNetHelper.invokeMethodAsync('HandleRightClick', c.x, c.y);
        else dotNetHelper.invokeMethodAsync('HandleMouseDown', c.x, c.y);
    };

    canvas.onmousemove = (e) => {
        const c = toMeters(e.clientX, e.clientY);
        dotNetHelper.invokeMethodAsync('HandleMouseMove', c.x, c.y);
    };

    canvas.onmouseup = () => dotNetHelper.invokeMethodAsync('HandleMouseUp');
    canvas.oncontextmenu = (e) => e.preventDefault();
};

window.drawCar = (ctx, car, toScreenX, toScreenY) => {
    const scale = window.gameCanvas.scale;
    const carLen = 4.0 * scale;
    const carWid = 2.0 * scale;

    ctx.save();
    ctx.translate(toScreenX(car.x), toScreenY(car.y));
    ctx.rotate(-car.theta);

    ctx.fillStyle = "blue";
    ctx.fillRect(-carLen / 2, -carWid / 2, carLen, carWid);

    // Direction indicator
    ctx.fillStyle = "yellow";
    ctx.beginPath();
    ctx.moveTo(0, 0);
    ctx.lineTo(carLen / 2, 0);
    ctx.stroke();

    ctx.restore();
};

window.clearCanvas = (canvasId) => {
    const canvas = document.getElementById(canvasId);
    const ctx = canvas.getContext('2d');
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    return { canvas, ctx };
};