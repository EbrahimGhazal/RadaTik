window.initReceiverMap = (function () {
    /** Bearing from north clockwise (radians). Matches dashboard coverage map. */
    function calculatePoint(center, radius, bearingRad) {
        var lat = center[0] + (radius / 111320) * Math.cos(bearingRad);
        var lng = center[1] + (radius / (111320 * Math.cos(center[0] * Math.PI / 180))) * Math.sin(bearingRad);
        return [lat, lng];
    }

    function formatNumber(value, digits) {
        var n = Number(value);
        if (!isFinite(n)) return "-";
        return n.toFixed(digits);
    }

    function toNumberOrDefault(value, fallback) {
        var n = Number(value);
        return isFinite(n) ? n : fallback;
    }

    function isFiniteNumber(value) {
        var n = Number(value);
        return isFinite(n);
    }

    return function initReceiverMap(containerId, mapDataJson, options) {
        options = options || {};

        if (typeof L === "undefined") return null;

        var element = document.getElementById(containerId);
        if (!element) return null;

        var sectors = [];
        if (typeof mapDataJson === "string") {
            try {
                sectors = JSON.parse(mapDataJson);
            } catch (e) {
                sectors = [];
            }
        } else if (Array.isArray(mapDataJson)) {
            sectors = mapDataJson;
        }

        // مركز افتراضي: اللاذقية، سوريا (عند عدم وجود بيانات أو قبل التمركز على العناصر)
        var defaultCenter = options.defaultCenter || [35.52, 35.78];
        var defaultZoom = typeof options.defaultZoom === "number" ? options.defaultZoom : 10;
        var map = L.map(containerId).setView(defaultCenter, defaultZoom);

        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: "&copy; OpenStreetMap"
        }).addTo(map);

        if (!sectors.length) return map;

        var firstValid = sectors.find(function (s) {
            return isFiniteNumber(s.latitude) && isFiniteNumber(s.longitude);
        });
        if (firstValid) {
            map.setView([firstValid.latitude, firstValid.longitude], defaultZoom);
        }

        var sectorIcon = L.AwesomeMarkers.icon({
            icon: "broadcast-tower",
            prefix: "fa",
            markerColor: "blue"
        });

        var receiverIcon = L.AwesomeMarkers.icon({
            icon: "wifi",
            prefix: "fa",
            markerColor: "green"
        });

        var bounds = [];

        sectors.forEach(function (s) {
            if (!isFiniteNumber(s.latitude) || !isFiniteNumber(s.longitude)) return;

            var direction = toNumberOrDefault(s.direction, 0);
            var angle = toNumberOrDefault(s.coverageAngle, 60);
            var range = toNumberOrDefault(s.coverageRange, 5);

            var sectorMarker = L.marker([s.latitude, s.longitude], { icon: sectorIcon }).addTo(map);
            sectorMarker.bindPopup(
                "<strong>المرسل: " + (s.name || "") + "</strong><br/>" +
                "عدد المستقبلات: " + (s.receivers ? s.receivers.length : 0) + "<hr style=\"margin:6px 0;\" />" +
                "خط العرض: " + formatNumber(s.latitude, 6) + "<br/>" +
                "خط الطول: " + formatNumber(s.longitude, 6) + "<br/>" +
                "الاتجاه: " + formatNumber(direction, 1) + "&deg;<br/>" +
                "زاوية الانتشار: " + formatNumber(angle, 1) + "&deg;<br/>" +
                "مدى الانتشار: " + formatNumber(range, 2) + " كم"
            );
            bounds.push([s.latitude, s.longitude]);

            var rangeMeters = range * 1000;
            var startAngleRad = (direction - angle / 2) * Math.PI / 180;
            var endAngleRad = (direction + angle / 2) * Math.PI / 180;
            var coveragePoints = [[s.latitude, s.longitude]];
            for (var i = 0; i <= 20; i++) {
                var currentAngle = startAngleRad + (endAngleRad - startAngleRad) * i / 20;
                var p = calculatePoint([s.latitude, s.longitude], rangeMeters, currentAngle);
                coveragePoints.push(p);
                bounds.push(p);
            }
            coveragePoints.push([s.latitude, s.longitude]);
            L.polygon(coveragePoints, {
                color: "#007bff",
                fillColor: "#007bff",
                fillOpacity: 0.15,
                weight: 1.5
            }).addTo(map);

            if (Array.isArray(s.receivers)) {
                s.receivers.forEach(function (r) {
                    if (!isFiniteNumber(r.latitude) || !isFiniteNumber(r.longitude)) return;

                    var recMarker = L.marker([r.latitude, r.longitude], { icon: receiverIcon }).addTo(map);
                    recMarker.bindPopup(
                        "<strong>المستقبل: " + (r.name || "") + "</strong><br/>" +
                        "IP: " + (r.ip || "") + "<br/>" +
                        "يتبع للمرسل: " + (s.name || "")
                    );

                    L.polyline([[s.latitude, s.longitude], [r.latitude, r.longitude]], {
                        color: "#4caf50",
                        weight: 2,
                        opacity: 0.6
                    }).addTo(map);

                    bounds.push([r.latitude, r.longitude]);
                });
            }
        });

        if (bounds.length > 0) {
            map.fitBounds(bounds, { padding: [20, 20] });
        }

        return map;
    };
})();
