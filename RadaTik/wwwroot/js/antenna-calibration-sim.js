(function () {
    function bindSim(root) {
        if (!root || root.getAttribute('data-bound') === '1') return;
        root.setAttribute('data-bound', '1');
        var stage = root.querySelector('.calibrate-sim-stage');
        var play = root.querySelector('.cal-sim-play');
        var ok = root.querySelector('.cal-sim-ok');
        var bad = root.querySelector('.cal-sim-bad');
        var timer = null;

        function clearModes() {
            stage.classList.remove('is-play', 'is-ok', 'is-bad');
        }

        function setOk() {
            clearModes();
            stage.classList.add('is-ok');
        }

        function setBad() {
            clearModes();
            stage.classList.add('is-bad');
        }

        function playSim() {
            clearModes();
            stage.classList.add('is-play');
            if (timer) clearTimeout(timer);
            timer = setTimeout(function () {
                stage.classList.remove('is-play');
                stage.classList.add('is-ok');
            }, 3200);
        }

        if (play) play.addEventListener('click', playSim);
        if (ok) ok.addEventListener('click', setOk);
        if (bad) bad.addEventListener('click', setBad);

        // auto demo once when visible
        setTimeout(playSim, 400);
    }

    function initAll() {
        document.querySelectorAll('.calibrate-sim').forEach(bindSim);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAll);
    } else {
        initAll();
    }
})();
