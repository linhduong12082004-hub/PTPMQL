$(function () {
    const modalElement = document.getElementById("crudModal");

    if (!modalElement) {
        return;
    }

    const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
    const modalContent = $(modalElement).find(".modal-content");

    function showError(message) {
        const alert = $("<div>", {
            class: "alert alert-danger m-3",
            role: "alert"
        }).text(message);

        modalContent.empty().append(alert);
    }

    function parseValidation(form) {
        if ($.validator && $.validator.unobtrusive) {
            $.validator.unobtrusive.parse(form);
        }
    }

    $(document).on("click", "[data-modal-url]", function (event) {
        event.preventDefault();
        modalContent.html('<div class="modal-body text-center py-5"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Đang tải...</span></div></div>');
        modal.show();

        $.ajax({
            url: this.href,
            method: "GET",
            headers: { "X-Requested-With": "XMLHttpRequest" }
        }).done(function (html) {
            modalContent.html(html);
            parseValidation(modalContent.find("form"));
        }).fail(function (xhr) {
            console.error("Không thể tải nội dung CRUD:", xhr.status, xhr.statusText);
            showError("Không thể tải dữ liệu. Vui lòng thử lại.");
        });
    });

    $(document).on("submit", "[data-ajax-modal-form]", function (event) {
        event.preventDefault();

        const form = this;
        const $form = $(form);
        if ($form.data("validator") && !$form.valid()) {
            return;
        }

        const submitButtons = $form.find(":submit");
        submitButtons.prop("disabled", true);

        $.ajax({
            url: form.action,
            method: form.method || "POST",
            data: $form.serialize(),
            dataType: "json",
            headers: { "X-Requested-With": "XMLHttpRequest" }
        }).done(function (response) {
            if (response && response.success) {
                modal.hide();
                $.get(window.location.href)
                    .done(function (html) {
                        const currentPage = $("<div>").html(html);
                        const updatedContent = currentPage.find("main").html();
                        if (updatedContent) {
                            $("main").html(updatedContent);
                        }
                    })
                    .fail(function () {
                        showError("Đã lưu dữ liệu nhưng không thể cập nhật danh sách. Vui lòng tải lại trang.");
                    });
                return;
            }
            showError(response && response.message
                ? response.message
                : "Không thể hoàn tất thao tác. Vui lòng thử lại.");
        }).fail(function (xhr) {
            if (xhr.status === 400 && xhr.responseText) {
                let jsonResponse;
                try {
                    jsonResponse = JSON.parse(xhr.responseText);
                } catch (_) {
                    jsonResponse = null;
                }

                if (jsonResponse && jsonResponse.message) {
                    showError(jsonResponse.message);
                } else {
                    modalContent.html(xhr.responseText);
                    parseValidation(modalContent.find("form"));
                }
                return;
            }

            console.error("Yêu cầu CRUD thất bại:", xhr.status, xhr.statusText);
            showError("Đã xảy ra lỗi khi lưu dữ liệu. Vui lòng thử lại.");
        }).always(function () {
            submitButtons.prop("disabled", false);
        });
    });
});
