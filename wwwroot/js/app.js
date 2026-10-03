function getUser(){try{return JSON.parse(localStorage.getItem("smartUser")||"null")}catch{return null}}
function guardUser(){const u=getUser();if(!u){location.href="/login.html";return null}return u}
function guardAdmin(){const u=guardUser();if(u&&u.role!=="Admin")location.href="/dashboard.html";return u}
function logout(){localStorage.removeItem("smartUser");location.href="/login.html"}
async function api(url,method="GET",body=null){
  return fetch(url,{method,headers:body?{"Content-Type":"application/json"}:undefined,body:body?JSON.stringify(body):undefined})
}
function money(n){return "₹"+Number(n||0).toLocaleString("en-IN",{maximumFractionDigits:2})}
function renderNav(){
 const u=getUser(); if(!u)return;
 const links=u.role==="Admin"
 ? `<a href="/admin.html">Admin</a>`
 : `<a href="/dashboard.html">Home</a><a href="/expenses.html">Expenses</a><a href="/budgets.html">Budgets</a><a href="/goals.html">Goals</a><a href="/analytics.html">Analytics</a><a href="/advisor.html">AI Adviser</a>`;
 const el=document.querySelector("#appNav");
 if(el)el.innerHTML=`<nav class="nav glass"><a class="brand" href="${u.role==="Admin"?"/admin.html":"/dashboard.html"}">Smart<i>Spend</i></a><div class="navlinks">${links}<button class="btn secondary" onclick="logout()">Logout</button></div></nav>`;
}
