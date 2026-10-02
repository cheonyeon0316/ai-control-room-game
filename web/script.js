(() => {
  "use strict";

  const dialog = document.querySelector(".lightbox");
  // Without dialog support, the screenshot links still open the original files.
  if (!dialog || typeof dialog.showModal !== "function") return;

  const captures = [
    {
      src: "./assets/control-room.png",
      title: "01 / 관제 화면",
      alt: "네 대의 CCTV, 요원 상태, 작전 기록과 자연어 명령 입력창이 있는 실제 관제 화면",
      caption: "네 대의 CCTV를 관찰하고 담당자를 선택해 자연어 명령을 보냅니다."
    },
    {
      src: "./assets/field-photo.png",
      title: "02 / 현장 사진",
      alt: "현장 요원이 실제 카메라로 촬영한 연구실의 닫힌 캐비닛",
      caption: "사진 명령으로 촬영한 현장 이미지가 증거 자료에 저장됩니다."
    },
    {
      src: "./assets/mission-result.png",
      title: "03 / 작전 결과",
      alt: "작전 성공과 관찰된 지휘 행동을 정리한 게임 결과 화면",
      caption: "미션 결과와 명령, 확인 질문, 근거 요청 등 관찰된 행동을 확인합니다."
    }
  ];
  const image = dialog.querySelector(".lightbox-image");
  const title = dialog.querySelector("#lightbox-title");
  const caption = dialog.querySelector(".lightbox-caption");
  const count = dialog.querySelector(".lightbox-count");
  const original = dialog.querySelector(".lightbox-original");
  const close = dialog.querySelector(".lightbox-close");
  let activeIndex = 0;
  let returnFocus = null;

  function showCapture(index) {
    activeIndex = (index + captures.length) % captures.length;
    const capture = captures[activeIndex];
    image.src = capture.src;
    image.alt = capture.alt;
    title.textContent = capture.title;
    caption.textContent = capture.caption;
    count.textContent = `${String(activeIndex + 1).padStart(2, "0")} / 03`;
    original.href = capture.src;
  }

  document.querySelectorAll("[data-capture]").forEach((link) => {
    link.addEventListener("click", (event) => {
      if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) return;
      event.preventDefault();
      returnFocus = link;
      showCapture(Number(link.dataset.capture));
      dialog.showModal();
      document.body.classList.add("modal-open");
      close.focus();
    });
  });

  close.addEventListener("click", () => dialog.close());
  dialog.querySelector(".lightbox-prev").addEventListener("click", () => showCapture(activeIndex - 1));
  dialog.querySelector(".lightbox-next").addEventListener("click", () => showCapture(activeIndex + 1));
  dialog.addEventListener("keydown", (event) => {
    if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
      event.preventDefault();
      showCapture(activeIndex + (event.key === "ArrowLeft" ? -1 : 1));
    }
  });
  dialog.addEventListener("click", (event) => {
    if (event.target !== dialog) return;
    const bounds = dialog.getBoundingClientRect();
    const isOutside = event.clientX < bounds.left || event.clientX > bounds.right
      || event.clientY < bounds.top || event.clientY > bounds.bottom;
    if (isOutside) dialog.close();
  });
  dialog.addEventListener("close", () => {
    document.body.classList.remove("modal-open");
    if (returnFocus) returnFocus.focus({ preventScroll: true });
  });
})();
