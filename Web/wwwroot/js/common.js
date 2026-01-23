window.gameCanvas = {
    scale: 4.5
};

window.initCanvasEvents = (canvasId, dotNetHelper) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    // Size the canvas to fill its container
    const resizeCanvas = () => {
        const wrapper = canvas.parentElement;
        const rect = wrapper.getBoundingClientRect();

        // Account for wrapper padding (1.5rem = 24px on each side)
        const availableWidth = rect.width - 48;
        const availableHeight = rect.height - 48;

        // Make canvas fill the available space
        canvas.width = availableWidth;
        canvas.height = availableHeight;
    };

    resizeCanvas();
    window.addEventListener('resize', resizeCanvas);

    const scale = window.gameCanvas.scale;

    const toMeters = (clientX, clientY) => {
        const rect = canvas.getBoundingClientRect();
        const canvasCenterX = canvas.width / 2;
        const canvasCenterY = canvas.height / 2;

        // Get mouse position relative to canvas
        const canvasX = clientX - rect.left;
        const canvasY = clientY - rect.top;

        // Convert to world coordinates (meters)
        const x = (canvasX - canvasCenterX) / scale;
        const y = -(canvasY - canvasCenterY) / scale;

        return { x, y };
    };

    canvas.onmousedown = (e) => {
        const c = toMeters(e.clientX, e.clientY);
        if (e.button === 2) {
            dotNetHelper.invokeMethodAsync('HandleRightClick', c.x, c.y);
        } else {
            dotNetHelper.invokeMethodAsync('HandleMouseDown', c.x, c.y);
        }
    };

    canvas.onmousemove = (e) => {
        const c = toMeters(e.clientX, e.clientY);
        dotNetHelper.invokeMethodAsync('HandleMouseMove', c.x, c.y);
    };

    canvas.onmouseup = () => {
        dotNetHelper.invokeMethodAsync('HandleMouseUp');
    };

    canvas.oncontextmenu = (e) => {
        e.preventDefault();
    };
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