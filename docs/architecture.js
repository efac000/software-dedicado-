"use strict";
(async () => {
  const hint = document.getElementById("architecture-hint");
  if (!hint) return;
  const data = navigator.userAgentData;
  if (!data || data.platform !== "Windows" || typeof data.getHighEntropyValues !== "function") return;
  try {
    const { architecture, bitness, wow64 } = await data.getHighEntropyValues(["architecture", "bitness", "wow64"]);
    let recommended = null;
    if (architecture === "arm" && bitness === "64") recommended = "ARM64";
    else if (architecture === "x86" && (bitness === "64" || wow64 === true)) recommended = "x64";
    else if (architecture === "x86" && bitness === "32" && wow64 === false) recommended = "x86";
    if (recommended) hint.textContent = "Arquitetura indicada pelo navegador: Windows " + recommended + ". Confirme em Configurações > Sistema > Sobre antes de baixar. Todas as opções permanecem disponíveis.";
  } catch { /* Manual selection remains available. */ }
})();