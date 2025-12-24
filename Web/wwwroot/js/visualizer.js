window.drawSimulation = (canvasId, car, obstacles) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const ctx = canvas.getContext('2d');

    // 1. Clear Canvas
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    // 2. Center coordinate system (0,0 is target)
    const centerX = canvas.width / 2;
    const centerY = canvas.height / 2;
    const scale = 5; // Pixels per meter

    const toX = (val) => centerX + (val * scale);
    const toY = (val) => centerY - (val * scale); // Flip Y for standard Cartesian

    // 3. Draw Target (0,0)
    ctx.fillStyle = "green";
    ctx.beginPath();
    ctx.arc(toX(0), toY(0), 5, 0, Math.PI * 2);
    ctx.fill();

    // 4. Draw Obstacles
    ctx.fillStyle = "rgba(255, 0, 0, 0.5)";
    obstacles.forEach(obs => {
        ctx.beginPath();
        ctx.arc(toX(obs.x), toY(obs.y), obs.radius * scale, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = "red";
        ctx.stroke();
    });

    // 5. Draw Car
    if (car) {
        ctx.save();
        ctx.translate(toX(car.x), toY(car.y));
        ctx.rotate(-car.theta); // Negative because canvas rotation is clockwise

        // Car Body
        ctx.fillStyle = "blue";
        ctx.fillRect(-10, -5, 20, 10); // 4m x 2m car (scaled)

        // Front Direction Indicator
        ctx.fillStyle = "black";
        ctx.fillRect(6, -2, 4, 4); 
        ctx.restore();
    }
};

let draggedObstacleIndex = -1;

// Helper to convert pixels to meters (inverse of the draw scale)
function toMeters(pixelValue, offset, scale) {
    return (pixelValue - offset) / scale;
}

window.initCanvasEvents = (canvasId, dotNetHelper) => {
    const canvas = document.getElementById(canvasId);
    const scale = 5;
    const centerX = canvas.width / 2;
    const centerY = canvas.height / 2;

    canvas.onmousedown = (e) => {
        const rect = canvas.getBoundingClientRect();
        const mouseX = toMeters(e.clientX - rect.left, centerX, scale);
        const mouseY = -toMeters(e.clientY - rect.top, centerY, scale); // Flip Y back

        // Check if we clicked an obstacle (simple radius check)
        dotNetHelper.invokeMethodAsync('HandleMouseDown', mouseX, mouseY);
    };

    canvas.onmousemove = (e) => {
        if (e.buttons !== 1) return; // Only if mouse is held down
        const rect = canvas.getBoundingClientRect();
        const mouseX = toMeters(e.clientX - rect.left, centerX, scale);
        const mouseY = -toMeters(e.clientY - rect.top, centerY, scale);

        dotNetHelper.invokeMethodAsync('HandleMouseMove', mouseX, mouseY);
    };

    canvas.oncontextmenu = (e) => {
        e.preventDefault(); // Stop the actual right-click menu
        const rect = canvas.getBoundingClientRect();
        const mouseX = toMeters(e.clientX - rect.left, centerX, scale);
        const mouseY = -toMeters(e.clientY - rect.top, centerY, scale);

        // Tell C# to try and delete an obstacle at this location
        dotNetHelper.invokeMethodAsync('HandleRightClick', mouseX, mouseY);
    };
};