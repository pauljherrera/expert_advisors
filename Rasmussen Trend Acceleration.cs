using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class RasmussenTrendAcceleration : Robot
    {
        /*
        Indicaciones generales:
        - Este bot busca captar los aumentos de la velocidad en el mercado. Como se necesita medir
        la aceleración por segundos y no por minutos, no se puede usar OnBar() para esto.
        - El bot calculará cada X segundos cuántos pips se ha movido el precio. Partiendo de 
        ahí, calculara la velocidad del mercado en pips por segundo. La velocidad del mercado
        toma números negativos cuando el precio es bajista.
        - Partiendo de la velocidad del mercado también se calculará la aceleración del mercado. Por
        ejemplo, si la velocidad anterior era de 2 pips/segundo y la velocidad actual es de 3 pips
        por segundo, se dirá que la aceleración del mercado es de 1.5 (Es decir, 1.5 veces la medida anterior)
        - El usuario establecerá un umbral Y de aceleración. Cuando el umbral Y se alcanza o traspasa, el bot
        entra en la fase de dos.
        - Para la fase 2, el usuario elige un tiempo Z. Cuando la aceleración del mercado sea 
        igual a Y por Z segundos, el bot entrará en la dirección del mercado.
        - La dirección del mercado se determinará según el signo de la velocidad del mercado.
        - Para salir del mercado, se utilizarán los mismos parámetros, pero en vez de captar una aceleración
        del mercado, se busca captar una desaceleración del mercado. El usuario también puede elegir los 
        segundos de medición Xsalida, el umbral de aceleración Ysalida y el tiempo de aceleración Zsalida.
        
        */

        [Parameter(DefaultValue = 0.0)]
        public double Parameter { get; set; }

        /*Parámetros del usuario: 
        - Entry Evaluation Time.
        - Entry Acceleration Threshold.
        - Entry Acceleration Time.
        - Exit Evaluation Time.
        - Exit Acceleration Threshold.
        - Exit Acceleration Time.   */

        protected override void OnStart()
        {
            // Put your initialization logic here
        }

        protected override void OnTick()
        {
            //Si no se está en el mercado.

            // Evaluar si ya pasaron los X segundos para la siguiente evaluacion.

            //Si ya pasó el tiempo: calcular cuántos pips se movió el mercado, comparar con la velocidad
            //anterior para calcular la aceleración.

            // Si la aceleración es suficiente, pasar a la fase dos.


            //Si se está en fase dos...

            //Calcular cuánto tiempo ha pasado desde que se está en fase dos.

            //Si el tiempo es mayor al estipulado por el usuario, entrar al mercado.

            //Si se está en el mercado, seguír la lógica contraria, buscando captar la desaceleración del mercado.
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
        }
    }
}
