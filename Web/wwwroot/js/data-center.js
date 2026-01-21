// File: wwwroot/js/data-center.js
window.DataCenterUI = (function () {
    const module = {};
    module.heatArr = [];
    module.priceArr = [];
    module.steps = 24;
    module.dotNetRef = null;

    function makeCanvas(canvasId, arrRef, color) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        const w = canvas.width, h = canvas.height;
        let drawing = false;

        // initialize arrRef if empty
        for (let i = 0; i < module.steps; i++) if (arrRef[i] === undefined) arrRef[i] = 0.5;

        function render() {
            ctx.clearRect(0, 0, w, h);
            ctx.fillStyle = "#fff";
            ctx.fillRect(0, 0, w, h);

            // background grid
            ctx.strokeStyle = "#eee";
            ctx.lineWidth = 1;
            for (let i = 0; i <= 24; i += 6) {
                ctx.beginPath();
                ctx.moveTo(i / 24 * w, 0);
                ctx.lineTo(i / 24 * w, h);
                ctx.stroke();
            }
            // draw polyline
            ctx.strokeStyle = color;
            ctx.lineWidth = 2;
            ctx.beginPath();
            for (let i = 0; i < module.steps; i++) {
                const v = arrRef[i] ?? 0;
                const x = (i / (module.steps - 1)) * w;
                const y = h - v * h;
                if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
            }
            ctx.stroke();
        }

        function setFromMouse(ev) {
            const rect = canvas.getBoundingClientRect();
            const x = ev.clientX - rect.left;
            const y = ev.clientY - rect.top;
            const idx = Math.round((x / w) * (module.steps - 1));
            let v = 1 - (y / h);
            if (v < 0) v = 0;
            if (v > 1) v = 1;
            arrRef[idx] = v;
            render();
        }

        canvas.onmousedown = (e) => { drawing = true; setFromMouse(e); }
        canvas.onmousemove = (e) => { if (drawing) setFromMouse(e); }
        window.addEventListener('mouseup', () => { drawing = false; });
        render();
    }

    module.init = function (heatCanvasId, priceCanvasId, steps, dotNetRef) {
        module.steps = steps || 24;
        module.heatArr = new Array(module.steps).fill(0.6);
        module.priceArr = new Array(module.steps).fill(1.0);
        module.dotNetRef = dotNetRef || null;

        makeCanvas(heatCanvasId, module.heatArr, "tomato");
        makeCanvas(priceCanvasId, module.priceArr, "steelblue");
    };

    module.setPreset = function (presetName) {
        if (presetName === "busy") {
            for (let i = 0; i < module.steps; i++) {
                const hour = i % 24;
                module.heatArr[i] = (hour >= 8 && hour <= 18) ? 0.9 : 0.4;
                module.priceArr[i] = (hour >= 8 && hour <= 18) ? 1.7 : 0.5;
            }
        } else if (presetName === "quiet") {
            for (let i = 0; i < module.steps; i++) {
                module.heatArr[i] = 0.35;
                module.priceArr[i] = 0.45;
            }
        }
        // If you want immediate re-render you can re-init from .NET: DataCenterUI.init(...)
    };

    module.sendForecasts = async function () {
        if (!module.dotNetRef) {
            console.warn("DotNetRef not set on DataCenterUI");
            return;
        }
        // Pass plain arrays of numbers to .NET
        await module.dotNetRef.invokeMethodAsync('ReceiveForecasts', module.heatArr, module.priceArr);
    };

    return module;
})();
