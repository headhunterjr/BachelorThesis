window.gameCanvas = {
    scale: 4.5,
    origins: {},
    setTargetMode: {},
    clientDrawingMode: {},
    clientPoints: {},
    localRedraw: {}
};

window.initCanvasEvents = (canvasId, dotNetRef) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    const updateSizeAndScale = (width, height) => {
        canvas.width = width;
        canvas.height = height;
        const visibleWorldWidth = 220.0;
        const isMobile = width < 640;
        const baseScale = width / visibleWorldWidth;
        window.gameCanvas.scale = isMobile ? baseScale * 1.5 : baseScale;

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
        const currentScale = window.gameCanvas.scale || (rect.width / visibleWorldWidth);

        const origin = window.gameCanvas.origins[canvasId] || { x: rect.width / 2, y: rect.height / 2 };

        const canvasX = clientX - rect.left;
        const canvasY = clientY - rect.top;

        const wx = (canvasX - origin.x) / currentScale;
        const wy = -(canvasY - origin.y) / currentScale;

        return { worldX: wx, worldY: wy, canvasX, canvasY };
    };

    const handleMouseDown = (e) => {
        const { worldX, worldY, canvasX, canvasY } = toWorld(e.clientX, e.clientY);

        if (window.gameCanvas.setTargetMode[canvasId]) {
            window.gameCanvas.origins[canvasId] = { x: canvasX, y: canvasY };
            window.gameCanvas.setTargetMode[canvasId] = false;
            try { dotNetRef.invokeMethodAsync('Redraw'); } catch (e) { }
            return;
        }

        if (e.button === 2) {
            dotNetRef.invokeMethodAsync('HandleRightClick', worldX, worldY);
        }
        else {
            if (window.gameCanvas.clientDrawingMode[canvasId]) {
                window.gameCanvas.clientPoints[canvasId] = [{ x: worldX, y: worldY }];
                if (window.gameCanvas.localRedraw[canvasId]) {
                    window.gameCanvas.localRedraw[canvasId]();
                }
                return;
            }
            dotNetRef.invokeMethodAsync('HandleMouseDown', worldX, worldY);
        }
    };

    const handleMouseMove = (e) => {
        if (e.buttons === 0) return;
        const { worldX, worldY } = toWorld(e.clientX, e.clientY);

        if (window.gameCanvas.clientDrawingMode[canvasId] && e.buttons === 1) {
            if (!window.gameCanvas.clientPoints[canvasId]) {
                window.gameCanvas.clientPoints[canvasId] = [];
            }
            window.gameCanvas.clientPoints[canvasId].push({ x: worldX, y: worldY });
            if (window.gameCanvas.localRedraw[canvasId]) {
                window.gameCanvas.localRedraw[canvasId]();
            }
            return;
        }

        dotNetRef.invokeMethodAsync('HandleMouseMove', worldX, worldY);
    };

    const handleMouseUp = (e) => {
        if (e && e.button === 0 && window.gameCanvas.clientDrawingMode[canvasId]) {
            const points = window.gameCanvas.clientPoints[canvasId];
            if (points && points.length > 0) {
                dotNetRef.invokeMethodAsync('HandlePathDrawn', points.map(p => [p.x, p.y]));
            }
            window.gameCanvas.clientPoints[canvasId] = [];
            if (window.gameCanvas.localRedraw[canvasId]) {
                window.gameCanvas.localRedraw[canvasId]();
            }
            return;
        }
        dotNetRef.invokeMethodAsync('HandleMouseUp');
    };

    const handleContextMenu = (e) => {
        e.preventDefault();
    };

    let touchActive = false;

    const handleTouchStart = (e) => {
        e.preventDefault();
        touchActive = true;

        const touch = e.touches[0];
        const { worldX, worldY, canvasX, canvasY } = toWorld(touch.clientX, touch.clientY);

        if (window.gameCanvas.setTargetMode[canvasId]) {
            window.gameCanvas.origins[canvasId] = { x: canvasX, y: canvasY };
            window.gameCanvas.setTargetMode[canvasId] = false;
            try { dotNetRef.invokeMethodAsync('Redraw'); } catch (e) { }
            return;
        }

        if (window.gameCanvas.clientDrawingMode[canvasId]) {
            window.gameCanvas.clientPoints[canvasId] = [{ x: worldX, y: worldY }];
            if (window.gameCanvas.localRedraw[canvasId]) {
                window.gameCanvas.localRedraw[canvasId]();
            }
            return;
        }

        dotNetRef.invokeMethodAsync('HandleMouseDown', worldX, worldY);
    };

    const handleTouchMove = (e) => {
        e.preventDefault();
        if (!touchActive) return;

        const touch = e.touches[0];
        const { worldX, worldY } = toWorld(touch.clientX, touch.clientY);

        if (window.gameCanvas.clientDrawingMode[canvasId]) {
            if (!window.gameCanvas.clientPoints[canvasId]) {
                window.gameCanvas.clientPoints[canvasId] = [];
            }
            window.gameCanvas.clientPoints[canvasId].push({ x: worldX, y: worldY });
            if (window.gameCanvas.localRedraw[canvasId]) {
                window.gameCanvas.localRedraw[canvasId]();
            }
            return;
        }

        dotNetRef.invokeMethodAsync('HandleMouseMove', worldX, worldY);
    };

    const handleTouchEnd = (e) => {
        e.preventDefault();
        if (!touchActive) return;
        touchActive = false;

        if (window.gameCanvas.clientDrawingMode[canvasId]) {
            const points = window.gameCanvas.clientPoints[canvasId];
            if (points && points.length > 0) {
                dotNetRef.invokeMethodAsync('HandlePathDrawn', points.map(p => [p.x, p.y]));
            }
            window.gameCanvas.clientPoints[canvasId] = [];
            if (window.gameCanvas.localRedraw[canvasId]) {
                window.gameCanvas.localRedraw[canvasId]();
            }
            return;
        }

        dotNetRef.invokeMethodAsync('HandleMouseUp');
    };

    canvas.addEventListener('mousedown', handleMouseDown);
    canvas.addEventListener('mousemove', handleMouseMove);
    canvas.addEventListener('mouseup', handleMouseUp);
    canvas.addEventListener('contextmenu', handleContextMenu);

    canvas.addEventListener('touchstart', handleTouchStart, { passive: false });
    canvas.addEventListener('touchmove', handleTouchMove, { passive: false });
    canvas.addEventListener('touchend', handleTouchEnd, { passive: false });
    canvas.addEventListener('touchcancel', handleTouchEnd, { passive: false });

    canvas._eventHandlers = {
        mousedown: handleMouseDown,
        mousemove: handleMouseMove,
        mouseup: handleMouseUp,
        contextmenu: handleContextMenu,
        touchstart: handleTouchStart,
        touchmove: handleTouchMove,
        touchend: handleTouchEnd,
        touchcancel: handleTouchEnd
    };
};

window.setClientSideDrawing = (canvasId, enabled) => {
    window.gameCanvas.clientDrawingMode[canvasId] = !!enabled;
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

        if (canvas._eventHandlers) {
            const h = canvas._eventHandlers;
            canvas.removeEventListener('mousedown', h.mousedown);
            canvas.removeEventListener('mousemove', h.mousemove);
            canvas.removeEventListener('mouseup', h.mouseup);
            canvas.removeEventListener('contextmenu', h.contextmenu);
            canvas.removeEventListener('touchstart', h.touchstart);
            canvas.removeEventListener('touchmove', h.touchmove);
            canvas.removeEventListener('touchend', h.touchend);
            canvas.removeEventListener('touchcancel', h.touchcancel);
            delete canvas._eventHandlers;
        }

        delete window.gameCanvas.origins[canvasId];
        delete window.gameCanvas.setTargetMode[canvasId];
        delete window.gameCanvas.clientDrawingMode[canvasId];
        delete window.gameCanvas.clientPoints[canvasId];
        delete window.gameCanvas.localRedraw[canvasId];
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

window.downloadCsv = (baseName, content) => {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    const seconds = String(now.getSeconds()).padStart(2, '0');

    const timestamp = `${year}-${month}-${day}_${hours}-${minutes}-${seconds}`;
    const fileName = `${baseName}_${timestamp}.csv`;

    const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.target = "_blank";
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};