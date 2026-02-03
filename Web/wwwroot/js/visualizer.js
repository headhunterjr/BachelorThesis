window.drawSimulation = (canvasId, car, obstacles) => {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const ctx = canvas.getContext('2d');

    ctx.clearRect(0, 0, canvas.width, canvas.height);

    const centerX = canvas.width / 2;
    const centerY = canvas.height / 2;
    const scale = 5;

    const toX = (val) => centerX + (val * scale);
    const toY = (val) => centerY - (val * scale);

    ctx.fillStyle = "green";
    ctx.beginPath();
    ctx.arc(toX(0), toY(0), 5, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = "rgba(255, 0, 0, 0.5)";
    obstacles.forEach(obs => {
        ctx.beginPath();
        ctx.arc(toX(obs.x), toY(obs.y), obs.radius * scale, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = "red";
        ctx.stroke();
    });

    if (car) {
        ctx.save();
        ctx.translate(toX(car.x), toY(car.y));
        ctx.rotate(-car.theta);

        ctx.fillStyle = "blue";
        ctx.fillRect(-10, -5, 20, 10);

        ctx.fillStyle = "black";
        ctx.fillRect(6, -2, 4, 4); 
        ctx.restore();
    }
};

let draggedObstacleIndex = -1;

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
        const mouseY = -toMeters(e.clientY - rect.top, centerY, scale);

        dotNetHelper.invokeMethodAsync('HandleMouseDown', mouseX, mouseY);
    };

    canvas.onmousemove = (e) => {
        if (e.buttons !== 1) return;
        const rect = canvas.getBoundingClientRect();
        const mouseX = toMeters(e.clientX - rect.left, centerX, scale);
        const mouseY = -toMeters(e.clientY - rect.top, centerY, scale);

        dotNetHelper.invokeMethodAsync('HandleMouseMove', mouseX, mouseY);
    };

    canvas.oncontextmenu = (e) => {
        e.preventDefault();
        const rect = canvas.getBoundingClientRect();
        const mouseX = toMeters(e.clientX - rect.left, centerX, scale);
        const mouseY = -toMeters(e.clientY - rect.top, centerY, scale);

        dotNetHelper.invokeMethodAsync('HandleRightClick', mouseX, mouseY);
    };
};