window.gameCanvas = {
    scale: 4.5
};

window.initCanvasEvents = (canvasId, dotNetRef) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    // --- NEW: Resize Handler ---
    const resizeHandler = () => {
        // 1. Resize internal memory to match CSS display size
        const displayWidth = canvas.clientWidth;
        const displayHeight = canvas.clientHeight;
        
        if (canvas.width !== displayWidth || canvas.height !== displayHeight) {
            canvas.width = displayWidth;
            canvas.height = displayHeight;
        }

        // 2. Ask C# to redraw immediately so the canvas isn't blank
        try {
            dotNetRef.invokeMethodAsync('Redraw');
        } catch (e) {
            // Ignored: Component might be disposed
        }
    };

    // Debounce resize to prevent lag
    let resizeTimeout;
    const onResize = () => {
        clearTimeout(resizeTimeout);
        resizeTimeout = setTimeout(resizeHandler, 20); // 20ms delay
    };

    window.addEventListener('resize', onResize);
    // Store cleanup function for later
    canvas._cleanupResize = () => window.removeEventListener('resize', onResize);

    // --- Existing Mouse Handling (Keep your existing mouse logic here!) ---
    // Make sure to add the mouse event listeners here as before...
    canvas.onmousedown = (e) => {
        const rect = canvas.getBoundingClientRect();
        const scale = window.gameCanvas?.scale || 20;
        const simX = (e.clientX - rect.left - canvas.width/2) / scale;
        const simY = -(e.clientY - rect.top - canvas.height/2) / scale;
        dotNetRef.invokeMethodAsync('HandleMouseDown', simX, simY);
    };
    
    canvas.onmousemove = (e) => {
        const rect = canvas.getBoundingClientRect();
        const scale = window.gameCanvas?.scale || 20;
        const simX = (e.clientX - rect.left - canvas.width/2) / scale;
        const simY = -(e.clientY - rect.top - canvas.height/2) / scale;
        dotNetRef.invokeMethodAsync('HandleMouseMove', simX, simY);
    };

    canvas.onmouseup = () => dotNetRef.invokeMethodAsync('HandleMouseUp');
    
    canvas.oncontextmenu = (e) => {
        e.preventDefault();
        const rect = canvas.getBoundingClientRect();
        const scale = window.gameCanvas?.scale || 20;
        const simX = (e.clientX - rect.left - canvas.width/2) / scale;
        const simY = -(e.clientY - rect.top - canvas.height/2) / scale;
        dotNetRef.invokeMethodAsync('HandleRightClick', simX, simY);
        return false;
    };
};

// --- NEW: Cleanup Function ---
window.disposeCanvasEvents = (canvasId) => {
    const canvas = document.getElementById(canvasId);
    if (canvas) {
        if (canvas._cleanupResize) canvas._cleanupResize();
        canvas.onmousedown = null;
        canvas.onmousemove = null;
        canvas.onmouseup = null;
        canvas.oncontextmenu = null;
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