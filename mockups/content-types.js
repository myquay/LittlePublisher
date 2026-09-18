// Mirrors ContentTypeCatalog; fields are [property, label, input type, required to publish].
const contentTypes={
 article:{label:'Article',hint:'A longer piece, with room to develop an idea.',fields:[]},
 note:{label:'Note',hint:'A short update. A title is optional.',fields:[]},
 photo:{label:'Photo',hint:'A photograph, with a caption if you like.',media:'photo',accept:'image/jpeg,image/png,image/gif,image/webp',fields:[['photo','Photo URL','text',true],['alt','Alternative text','text',true],['location','Location','text',false]]},
 activity:{label:'Activity',hint:'Record something you’ve been doing.',fields:[]},
 thought:{label:'Thought',hint:'An idea worth keeping.',fields:[]},
 reply:{label:'Reply',hint:'Continue a conversation.',fields:[['in-reply-to','Reply to URL','text',true],['reply-to-title','Original post title','text',false]]},
 like:{label:'Like',hint:'Keep a little appreciation for something.',fields:[['like-of','Liked URL','text',true]]},
 repost:{label:'Repost',hint:'Share something from elsewhere.',fields:[['repost-of','Original URL','text',true]]},
 bookmark:{label:'Bookmark',hint:'Save a link worth returning to.',fields:[['bookmark-of','Bookmark URL','text',true]]},
 blogroll:{label:'Blogroll entry',hint:'Add a site you enjoy reading.',fields:[['url','Website URL','text',true],['feed','Feed URL','text',false]]},
 event:{label:'Event',hint:'Something happening, somewhere.',fields:[['start','Starts','datetime-local',true],['end','Ends','datetime-local',false],['location','Location','text',false]]},
 audio:{label:'Audio',hint:'A recording, with a few words alongside it.',media:'audio',accept:'audio/mpeg,audio/ogg',fields:[['audio','Audio URL','text',true],['alt','Description','text',false]]},
 video:{label:'Video',hint:'A moving picture and its story.',media:'video',accept:'video/mp4,video/webm',fields:[['video','Video URL','text',true],['alt','Description','text',false]]}
};
