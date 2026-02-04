window.gameCanvas = {
    scale: 4.5,
    origins: {},
    setTargetMode: {}
};

window.initCanvasEvents = (canvasId, dotNetRef) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    const updateSizeAndScale = (width, height) => {
        canvas.width = width;
        canvas.height = height;
        const visibleWorldWidth = 220.0;
        window.gameCanvas.scale = width / visibleWorldWidth;

        if (!window.gameCanvas.setTargetMode[canvasId]) {
            window.gameCanvas.origins[canvasId] = { x: width / 2, y: height / 2 };
        }
    };

    const resizeObserver = new ResizeObserver(entries => {
        for (const entry of entries) {
            const width = entry.contentRect ? entry.contentRect.width : canvas.clientWidth;
            const height = entry.contentRect ? entry.contentRect.height : canvas.clientHeight;

            if (width > 0 && height > 0) {
                updateSizeAndScale(width, height);
                try { dotNetRef.invokeMethodAsync('Redraw'); } catch (e) { }
            }
        }
    });

    resizeObserver.observe(canvas);
    canvas._cleanupResize = () => resizeObserver.disconnect();
    updateSizeAndScale(canvas.clientWidth, canvas.clientHeight);

    const toWorld = (clientX, clientY) => {
        const rect = canvas.getBoundingClientRect();

        const visibleWorldWidth = 220.0;
        const currentScale = rect.width / visibleWorldWidth;

        const origin = window.gameCanvas.origins[canvasId] || { x: rect.width / 2, y: rect.height / 2 };

        const canvasX = clientX - rect.left;
        const canvasY = clientY - rect.top;

        const wx = (canvasX - origin.x) / currentScale;
        const wy = -(canvasY - origin.y) / currentScale;

        return { worldX: wx, worldY: wy, canvasX, canvasY };
    };

    canvas.onmousedown = (e) => {
        const { worldX, worldY, canvasX, canvasY } = toWorld(e.clientX, e.clientY);

        if (window.gameCanvas.setTargetMode[canvasId]) {
            window.gameCanvas.origins[canvasId] = { x: canvasX, y: canvasY };
            window.gameCanvas.setTargetMode[canvasId] = false;
            try { dotNetRef.invokeMethodAsync('Redraw'); } catch (e) { }
            return;
        }

        if (e.button === 2) {
            dotNetRef.invokeMethodAsync('HandleRightClick', worldX, worldY);
        } else {
            dotNetRef.invokeMethodAsync('HandleMouseDown', worldX, worldY);
        }
    };

    canvas.onmousemove = (e) => {
        if (e.buttons === 0) return; 
        const { worldX, worldY } = toWorld(e.clientX, e.clientY);
        dotNetRef.invokeMethodAsync('HandleMouseMove', worldX, worldY);
    };

    canvas.onmouseup = () => {
        dotNetRef.invokeMethodAsync('HandleMouseUp');
    };

    canvas.oncontextmenu = (e) => {
        e.preventDefault();
    };
};

window.setCanvasSetTargetMode = (canvasId, enabled) => {
    if (!window.gameCanvas.origins[canvasId]) {
        const canvas = document.getElementById(canvasId);
        if (canvas) {
            window.gameCanvas.origins[canvasId] = { x: canvas.width / 2, y: canvas.height / 2 };
        }
    }
    window.gameCanvas.setTargetMode[canvasId] = !!enabled;
};

window.resetCanvasOrigin = (canvasId) => {
    const canvas = document.getElementById(canvasId);
    if (canvas) {
        window.gameCanvas.origins[canvasId] = { x: canvas.width / 2, y: canvas.height / 2 };
        window.gameCanvas.setTargetMode[canvasId] = false;
    }
};

window.disposeCanvasEvents = (canvasId) => {
    const canvas = document.getElementById(canvasId);
    if (canvas) {
        if (canvas._cleanupResize) canvas._cleanupResize();
        canvas.onmousedown = null;
        canvas.onmousemove = null;
        canvas.onmouseup = null;
        canvas.oncontextmenu = null;

        delete window.gameCanvas.origins[canvasId];
        delete window.gameCanvas.setTargetMode[canvasId];
    }
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

    ctx.fillStyle = "yellow";
    ctx.beginPath();
    ctx.moveTo(0, 0);
    ctx.lineTo(carLen / 2, 0);
    ctx.stroke();

    ctx.restore();
};

window.clearCanvas = (canvasId) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return { canvas: null, ctx: null };

    const displayWidth = canvas.clientWidth;
    const displayHeight = canvas.clientHeight;

    if (canvas.width !== displayWidth || canvas.height !== displayHeight) {
        canvas.width = displayWidth;
        canvas.height = displayHeight;
    }

    const ctx = canvas.getContext('2d');
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    return { canvas, ctx };
};

window.downloadCsv = (filename, content) => {
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.target = "_blank";
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};