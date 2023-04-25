import Vue from 'vue'
import App from './App.vue'

Vue.config.productionTip = false

new Vue({
  render: h => h(App),
}).$mount('#app')


function f(){
  i = 1;         // js S2703

  for (j = 0; j < array.length; j++) {  // js S2703
    // ...
  }
}
