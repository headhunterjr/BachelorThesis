window.gameCanvas = {
    scale: 4.5,
    origins: {},
    setTargetMode: {}
};

window.initCanvasEvents = (canvasId, dotNetRef) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    const ensureSizeAndOrigin = () => {
        const displayWidth = canvas.clientWidth;
        const displayHeight = canvas.clientHeight;

        // If size changed, update dimensions AND reset origin to center
        if (canvas.width !== displayWidth || canvas.height !== displayHeight) {
            canvas.width = displayWidth;
            canvas.height = displayHeight;
            window.gameCanvas.origins[canvasId] = { x: canvas.width / 2, y: canvas.height / 2 };
        }

        // If origin doesn't exist yet, set to center
        if (!window.gameCanvas.origins[canvasId]) {
            window.gameCanvas.origins[canvasId] = { x: canvas.width / 2, y: canvas.height / 2 };
        }
    };

    ensureSizeAndOrigin();

    let resizeTimeout;
    const onResize = () => {
        clearTimeout(resizeTimeout);
        resizeTimeout = setTimeout(() => {
            ensureSizeAndOrigin();
            try { dotNetRef.invokeMethodAsync('Redraw'); } catch (e) { }
        }, 20);
    };
    window.addEventListener('resize', onResize);

    canvas._cleanupResize = () => window.removeEventListener('resize', onResize);

    const scale = window.gameCanvas.scale;

    const toWorld = (clientX, clientY) => {
        const rect = canvas.getBoundingClientRect();
        const origin = window.gameCanvas.origins[canvasId] || { x: canvas.width / 2, y: canvas.height / 2 };
        const canvasX = clientX - rect.left;
        const canvasY = clientY - rect.top;
        const wx = (canvasX - origin.x) / scale;
        const wy = -(canvasY - origin.y) / scale;
        return { worldX: wx, worldY: wy, canvasX, canvasY };
    };

    canvas.onmousedown = (e) => {
        const rect = canvas.getBoundingClientRect();
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
    // Ensure origin exists before we try to modify it
    if (!window.gameCanvas.origins[canvasId]) {
        const canvas = document.getElementById(canvasId);
        if (canvas) {
            window.gameCanvas.origins[canvasId] = { x: canvas.width / 2, y: canvas.height / 2 };
        }
    }
    window.gameCanvas.setTargetMode[canvasId] = !!enabled;
};

// --- NEW: Helper to force reset origin to center ---
window.resetCanvasOrigin = (canvasId) => {
    const canvas = document.getElementById(canvasId);
    if (canvas) {
        window.gameCanvas.origins[canvasId] = { x: canvas.width / 2, y: canvas.height / 2 };
        // Also ensure target mode is off
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

        // Clean up stored state so next time we visit, it starts fresh (Centered)
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