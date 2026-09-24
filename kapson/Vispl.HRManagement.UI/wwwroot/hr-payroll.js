// Browser-only helpers for the payroll page: files are built on the server, saved here.
window.hrPayroll = {
    download: function (fileName, content, mimeType) {
        const blob = new Blob([content], { type: mimeType || "text/csv;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    },

    // Binary files built on the server (Excel workbooks), passed as base64.
    downloadBase64: function (fileName, base64, mimeType) {
        const bytes = Uint8Array.from(atob(base64), function (c) { return c.charCodeAt(0); });
        const blob = new Blob([bytes], { type: mimeType || "application/octet-stream" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    },

    // Prints only the element matching the selector (the preview document or the active table).
    print: function (selector) {
        const target = selector ? document.querySelector(selector) : null;
        if (!target) {
            window.print();
            return;
        }

        document.body.classList.add("hr-printing");
        target.classList.add("hr-print-target");

        const done = function () {
            document.body.classList.remove("hr-printing");
            target.classList.remove("hr-print-target");
            window.removeEventListener("afterprint", done);
        };

        window.addEventListener("afterprint", done);
        window.print();
    }
};
