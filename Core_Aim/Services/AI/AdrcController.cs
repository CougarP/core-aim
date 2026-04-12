using System;

namespace Core_Aim.Services.AI
{
    /// <summary>
    /// Active Disturbance Rejection Controller (ADRC)
    /// Portado de C:\Projeto-ONNX-DML\src\ADRC.cpp
    ///
    /// Usa um Observador de Estado Estendido (ESO) de 3ª ordem para estimar e
    /// cancelar perturbações não medidas, tornando o rastreio robusto contra
    /// movimentos bruscos, variação de latência e jitter da detecção.
    ///
    /// Parâmetros-chave expostos ao usuário:
    ///   b0  — "Correção / Force"  (padrão 21.7 do projeto referência)
    ///   wN  — derivado de Kp (Response)
    /// </summary>
    public class AdrcController
    {
        // ── Ganhos do controlador ────────────────────────────────────────────
        private double _kp;   // wN²
        private double _kd;   // 2 · wN · sigma
        private double _b0;   // ganho do canal de controle (força de rejeição)

        // ── Ganhos do ESO (derivados do polo w0) ─────────────────────────────
        private readonly double _b1, _b2, _b3;

        // ── Estados internos do ESO ──────────────────────────────────────────
        private double _x1, _x2, _x3;
        private double _u; // última saída de controle

        private readonly double _dt;

        /// <summary>
        /// Inicializa o ADRC.
        /// </summary>
        /// <param name="w0">Largura de banda do observador (velocidade de estimação de distúrbios)</param>
        /// <param name="b0">Ganho do canal de controle (~21.7 no projeto referência)</param>
        /// <param name="wN">Frequência natural do controlador (velocidade de resposta)</param>
        /// <param name="sigma">Amortecimento (1.0 = criticamente amortecido)</param>
        /// <param name="dt">Passo de integração em segundos</param>
        public AdrcController(double w0, double b0, double wN, double sigma, double dt)
        {
            _dt = Math.Max(dt, 1e-6);
            _b0 = Math.Max(b0, 0.001);
            _kp = wN * wN;
            _kd = 2.0 * wN * sigma;

            // Alocação de polo de 3ª ordem em -w0 (identico à referência C++)
            _b1 = 3.0 * w0;
            _b2 = 3.0 * w0 * w0;
            _b3 = w0 * w0 * w0;
        }

        /// <summary>Atualiza o ganho de rejeição de distúrbios (auto-tuner usa isto).</summary>
        public void SetB0(double b0) => _b0 = Math.Max(b0, 0.001);

        /// <summary>
        /// Executa um passo do ADRC — mesma assinatura da referência C++:
        /// y = medição (saída do P-controller), yref = referência.
        /// </summary>
        public double Update(double y, double yref)
        {
            double err = y - _x1;

            // Integração de Euler do ESO
            double x1t = _x1 + _dt * (_x2 + _b1 * err);
            double x2t = _x2 + _dt * (_x3 + _b0 * _u + _b2 * err);
            double x3t = _x3 + _dt * (_b3 * err);

            _x1 = x1t;
            _x2 = x2t;
            _x3 = x3t;

            // Lei de controle: cancela distúrbio estimado
            double e = yref - _x1;
            _u = (1.0 / _b0) * (e * _kp - _kd * _x2 - _x3);

            return -_u;
        }

        public void Reset()
        {
            _x1 = _x2 = _x3 = _u = 0;
        }
    }
}
